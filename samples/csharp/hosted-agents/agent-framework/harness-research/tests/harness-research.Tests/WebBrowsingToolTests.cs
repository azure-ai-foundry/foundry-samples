// Copyright (c) Microsoft. All rights reserved.

using System.Net;
using System.Net.Sockets;
using System.Text;
using SampleApp;
using Xunit;

namespace HarnessResearch.Tests;

public sealed class WebBrowsingToolTests
{
    [Fact]
    public async Task DownloadUriAsync_UsesThePolicyValidatedAddress()
    {
        await using var server = new OneShotHttpServer("<html><body>pinned</body></html>");
        int resolutionCount = 0;
        var tool = new WebBrowsingTool(
            new WebBrowsingToolOptions { AllowedHosts = ["rebind.invalid"] },
            (host, cancellationToken) =>
            {
                Assert.Equal("rebind.invalid", host);
                resolutionCount++;
                return Task.FromResult(new[] { IPAddress.Loopback });
            });

        string result = await tool.DownloadUriAsync(
            $"http://rebind.invalid:{server.Port}/",
            TestContext.Current.CancellationToken);

        Assert.Equal("pinned", result);
        Assert.Equal(1, resolutionCount);
    }

    [Fact]
    public async Task DownloadUriAsync_PreservesTheOriginalHostHeader()
    {
        await using var server = new OneShotHttpServer("<html><body>host</body></html>");
        var tool = CreateTool(
            ["original-host.invalid"],
            (_, _) => Task.FromResult(new[] { IPAddress.Loopback }));

        await tool.DownloadUriAsync(
            $"http://original-host.invalid:{server.Port}/resource",
            TestContext.Current.CancellationToken);

        string requestHeaders = await server.RequestHeaders;
        Assert.Contains(
            $"Host: original-host.invalid:{server.Port}",
            requestHeaders,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadUriAsync_TriesEveryPolicyValidatedAddress()
    {
        await using var server = new OneShotHttpServer("<html><body>second</body></html>");
        var tool = CreateTool(
            ["multiple.invalid"],
            (_, _) => Task.FromResult(
                new[] { IPAddress.Parse("127.0.0.2"), IPAddress.Loopback }));

        string result = await tool.DownloadUriAsync(
            $"http://multiple.invalid:{server.Port}/",
            TestContext.Current.CancellationToken);

        Assert.Equal("second", result);
    }

    [Fact]
    public async Task DownloadUriAsync_RevalidatesRedirectDestinations()
    {
        await using var server = new OneShotHttpServer(
            "<html><body>redirect</body></html>",
            $"http://blocked.invalid:{GetUnusedPort()}/private");
        var resolvedHosts = new List<string>();
        var tool = new WebBrowsingTool(
            new WebBrowsingToolOptions
            {
                AllowedHosts = ["allowed.invalid"],
                AllowPublicNetworks = true,
            },
            (host, cancellationToken) =>
            {
                resolvedHosts.Add(host);
                return Task.FromResult(new[] { IPAddress.Loopback });
            });

        string result = await tool.DownloadUriAsync(
            $"http://allowed.invalid:{server.Port}/",
            TestContext.Current.CancellationToken);

        Assert.Contains(
            "resolves to a private/internal network address",
            result,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["allowed.invalid", "blocked.invalid"], resolvedHosts);
    }

    [Fact]
    public async Task DownloadUriAsync_BlocksTheUnspecifiedAddressAsNonPublic()
    {
        await using var server = new OneShotHttpServer("<html><body>local</body></html>");
        var tool = new WebBrowsingTool(
            new WebBrowsingToolOptions { AllowPublicNetworks = true },
            (_, _) => throw new InvalidOperationException("IP literals must not use DNS."));

        string result = await tool.DownloadUriAsync(
            $"http://0.0.0.0:{server.Port}/",
            TestContext.Current.CancellationToken);

        Assert.Contains(
            "resolves to a private/internal network address",
            result,
            StringComparison.OrdinalIgnoreCase);
    }

    private static WebBrowsingTool CreateTool(
        IReadOnlyList<string> allowedHosts,
        Func<string, CancellationToken, Task<IPAddress[]>> resolver) =>
        new(new WebBrowsingToolOptions { AllowedHosts = allowedHosts }, resolver);

    private static int GetUnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class OneShotHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _serverTask;
        private readonly TaskCompletionSource<string> _requestHeaders =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public OneShotHttpServer(string responseBody, string? redirectLocation = null)
        {
            this._listener = new TcpListener(IPAddress.Loopback, 0);
            this._listener.Start();
            this.Port = ((IPEndPoint)this._listener.LocalEndpoint).Port;
            this._serverTask = this.ServeAsync(responseBody, redirectLocation);
        }

        public int Port { get; }

        public Task<string> RequestHeaders => this._requestHeaders.Task;

        public async ValueTask DisposeAsync()
        {
            await this._cancellationTokenSource.CancelAsync();
            this._listener.Stop();

            try
            {
                await this._serverTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException) when (this._cancellationTokenSource.IsCancellationRequested)
            {
            }

            this._cancellationTokenSource.Dispose();
        }

        private async Task ServeAsync(string responseBody, string? redirectLocation)
        {
            using TcpClient client = await this._listener.AcceptTcpClientAsync(
                this._cancellationTokenSource.Token);
            await using NetworkStream stream = client.GetStream();

            var request = new StringBuilder();
            byte[] buffer = new byte[1024];
            while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                int bytesRead = await stream.ReadAsync(
                    buffer,
                    this._cancellationTokenSource.Token);
                if (bytesRead == 0)
                {
                    break;
                }

                request.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
            }

            this._requestHeaders.TrySetResult(request.ToString());

            string response = redirectLocation is null
                ? $"HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: {Encoding.UTF8.GetByteCount(responseBody)}\r\nConnection: close\r\n\r\n{responseBody}"
                : $"HTTP/1.1 302 Found\r\nLocation: {redirectLocation}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(
                Encoding.UTF8.GetBytes(response),
                this._cancellationTokenSource.Token);
        }
    }
}
