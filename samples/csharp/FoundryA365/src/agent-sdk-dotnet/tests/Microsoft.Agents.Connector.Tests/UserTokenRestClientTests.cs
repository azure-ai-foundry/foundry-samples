// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Connector.RestClients;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Microsoft.Agents.Connector.Tests
{
    public class UserTokenRestClientTests
    {
        private static readonly Uri Endpoint = new("http://localhost");
        private const string UserId = "user-id";
        private const string ConnectionName = "connection-name";
        private const string ChannelId = "channel-id:subchannel-id";
        private const string Code = "code";
        private const string Include = "include";
        private readonly string[] AadResourceUrls = ["resource-url"];
        private readonly TokenExchangeRequest TokenExchangeRequest = new();
        private static readonly Mock<HttpClient> MockHttpClient = new();
        private const string FwdUrl = "fwd-url";
        private const string State = "state";
        private const string FinalRedirect = "final-redirect";

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var client = UseClient();
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullFactory()
        {
            Assert.Throws<ArgumentNullException>(() => new RestUserTokenClient("appId", Endpoint, null, null, null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullEndpoint()
        {
            Assert.Throws<ArgumentNullException>(() => new RestUserTokenClient("appId", null, null, null, null, null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullTokenProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new RestUserTokenClient("appId", Endpoint, new Mock<IHttpClientFactory>().Object, null, null));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetUserTokenAsync(null, ConnectionName, ChannelId, "code", CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetUserTokenAsync(UserId, null, ChannelId, "code", CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldReturnToken()
        {
            var tokenResponse = new TokenResponse
            {
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="This is a fake token for unit testing.")]
                Token = "eyJhbGciOiJIUzI1NiJ9.eyJJc3N1ZXIiOiJJc3N1ZXIiLCJleHAiOjE3NDQ3NDAyMDAsImlhdCI6MTc0NDc0MDIwMH0.YU5txFNPoG_htI7FmdsnckgkA5S2Zv3Ju56RFw1XBfs"
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            var result = await client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, Code, CancellationToken.None);

            Assert.Equal(tokenResponse.Token, result.Token);
        }

        [Fact]
        public async Task GetTokenAsync_ShouldReturnNullOnNotFound()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));
            
            var client = UseClient();

            Assert.Null(await client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, Code, CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, Code, CancellationToken.None));
        }

        [Fact]
        public async Task SignOutAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.SignOutUserAsync(null, ConnectionName, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task SignOutAsync_ShouldReturnContent()
        {
            var content = new
            {
                Body = "body"
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(content))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            // will throw for test failure
            await client.SignOutUserAsync(UserId, ConnectionName, ChannelId, CancellationToken.None);
        }

        [Fact]
        public async Task SignOutAsync_ShouldReturnNullOnNoContent()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

            var client = UseClient();

            // no error should happen
            await client.SignOutUserAsync(UserId, ConnectionName, ChannelId, CancellationToken.None);
        }

        [Fact]
        public async Task SignOutAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(async () => await client.SignOutUserAsync(UserId, ConnectionName, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenStatusAsync(null, null, null, CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldReturnTokenStatus()
        {
            var tokenStatus = new List<TokenStatus>
            {
                new() {
                    HasToken = true
                }
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokenStatus))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            var status = await client.GetTokenStatusAsync(UserId, ConnectionName, ChannelId, CancellationToken.None);

            Assert.Single(status);
            Assert.True(status[0].HasToken);
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetTokenStatusAsync(UserId, ChannelId, Include, CancellationToken.None));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(null, ConnectionName, AadResourceUrls, "channelId", CancellationToken.None));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(UserId, null, AadResourceUrls, "channelId", CancellationToken.None));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldReturnTokens()
        {
            var tokens = new Dictionary<string, TokenResponse>();
            tokens.Add("firstToken", 
            new TokenResponse
            {
                Token = "test-token1"
            });
            tokens.Add("secondToken",
            new TokenResponse
            {
                Token = "test-token2"
            });
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokens))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            var result = await client.GetAadTokensAsync(UserId, ConnectionName, AadResourceUrls, ChannelId, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(tokens["firstToken"].Token, result["firstToken"].Token);
            Assert.Equal(tokens["secondToken"].Token, result["secondToken"].Token);
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetAadTokensAsync(UserId, ConnectionName, AadResourceUrls, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(null, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(UserId, null, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullChannelId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, null, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullExchangeRequest()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, null, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldReturnContent()
        {
            var tokenResponse = new TokenResponse
            {
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="This is a fake token for unit testing.")]
                Token = "eyJhbGciOiJIUzI1NiJ9.eyJJc3N1ZXIiOiJJc3N1ZXIiLCJleHAiOjE3NDQ3NDAyMDAsImlhdCI6MTc0NDc0MDIwMH0.YU5txFNPoG_htI7FmdsnckgkA5S2Zv3Ju56RFw1XBfs"
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            Assert.NotNull(await client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeTokenAsync_ShouldReturnContent()
        {
            var tokenResponse = new TokenResponse
            {
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="This is a fake token for unit testing.")]
                Token = "eyJhbGciOiJIUzI1NiJ9.eyJJc3N1ZXIiOiJJc3N1ZXIiLCJleHAiOjE3NDQ3NDAyMDAsImlhdCI6MTc0NDc0MDIwMH0.YU5txFNPoG_htI7FmdsnckgkA5S2Zv3Ju56RFw1XBfs",
                ConnectionName = ConnectionName
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(httpResponse);

            var client = UseClient();

            var response = await client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None);
            Assert.NotNull(response);
            Assert.Equal(ConnectionName, ((TokenResponse)response).ConnectionName);
            Assert.Equal(tokenResponse.Token, ((TokenResponse)response).Token);
        }

        [Fact]
        public async Task ExchangeTokenAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeTokenAsync_ErrorResponseFor400()
        {
            var errorResponse = new
            {
                Error = new Error { Message = "Error Message" }
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(JsonSerializer.Serialize(errorResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(httpResponse);

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(async () => await client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeTokenAsync_WithContent404()
        {
            var tokenResponse = new
            {
                ConnectionName = ConnectionName
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(httpResponse);

            var client = UseClient();

            var response = await client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None);
            Assert.IsAssignableFrom<TokenResponse>(response);
            Assert.Equal(ConnectionName, ((TokenResponse)response).ConnectionName);
        }

        [Fact]
        public async Task ExchangeTokenAsync_WithoutContent404()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(string.Empty)
                });

            var client = UseClient();

            var response = await client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None);
            Assert.Null(response);
        }


        [Fact]
        public async Task GetTokenOrSignInResourceAsync_ShouldThrowOnNullActivity()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenOrSignInResourceAsync(ConnectionName, null, Code, FinalRedirect, FwdUrl, CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenOrSignInResourceAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenOrSignInResourceAsync(null, new Activity(), Code, FinalRedirect, FwdUrl, CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenOrSignInResourceAsync_ShouldReturnTokenOrSignInResourceResponse()
        {
            // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="This is a fake token for unit testing.")]
            var token = "eyJhbGciOiJIUzI1NiJ9.eyJJc3N1ZXIiOiJJc3N1ZXIiLCJleHAiOjE3NDQ3NDAyMDAsImlhdCI6MTc0NDc0MDIwMH0.YU5txFNPoG_htI7FmdsnckgkA5S2Zv3Ju56RFw1XBfs";

            var responseContent = new TokenOrSignInResourceResponse
            {
                TokenResponse = new TokenResponse { Token = token },
                SignInResource = new SignInResource { SignInLink = "test-link" }
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(responseContent))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            var activity = new Activity()
            {
                From = new ChannelAccount() { Id = UserId },
                ChannelId = ChannelId
            };

            var result = await client.GetTokenOrSignInResourceAsync(ConnectionName, activity, Code, FinalRedirect, FwdUrl, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(responseContent.TokenResponse.Token, result.TokenResponse.Token);
            Assert.Equal(responseContent.SignInResource.SignInLink, result.SignInResource.SignInLink);
        }

        [Fact]
        public async Task TokenNotExchangeable()
        {
            var httpTokenResponse = new TokenResponse
            {
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="This is a fake token for unit testing.")]
                Token = "eyJhbGciOiJIUzI1NiJ9.eyJJc3N1ZXIiOiJJc3N1ZXIiLCJleHAiOjE3NDQ3NDAyMDAsImlhdCI6MTc0NDc0MDIwMH0.YU5txFNPoG_htI7FmdsnckgkA5S2Zv3Ju56RFw1XBfs"
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(httpTokenResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(httpResponse);

            var client = UseClient();

            var tokenResponse = await client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, null, CancellationToken.None);
            Assert.NotNull(tokenResponse);
            Assert.False(tokenResponse.IsExchangeable);
        }

        [Fact]
        public async Task TokenExchangeableV1()
        {
            var httpTokenResponse = new TokenResponse
            {
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="This is a fake token for unit testing.")]
                Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJhcGk6Ly8wMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDAiLCJ2ZXIiOiIxLjAiLCJhcHBpZCI6IjAwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMCJ9."
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(httpTokenResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(httpResponse);

            var client = UseClient();

            var tokenResponse = await client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, null, CancellationToken.None);
            Assert.NotNull(tokenResponse);
            Assert.True(tokenResponse.IsExchangeable);
        }

        [Fact]
        public async Task TokenExchangeableV2()
        {
            var httpTokenResponse = new TokenResponse
            {
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="This is a fake token for unit testing.")]
                Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDAiLCJ2ZXIiOiIyLjAiLCJhenAiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDAifQ."
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(httpTokenResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(httpResponse);

            var client = UseClient();

            var tokenResponse = await client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, null, CancellationToken.None);
            Assert.NotNull(tokenResponse);
            Assert.True(tokenResponse.IsExchangeable);
        }

        [Fact]
        public async Task GetTokenAsync_ShouldUseParentChannel()
        {
            var sendCalled = false;

            MockHttpClient
                .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .Callback<HttpRequestMessage, CancellationToken>((request,ct) =>
                {
                    sendCalled = true;
                    Assert.Contains($"channelId=channel-id", request.RequestUri.ToString());
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, Code, CancellationToken.None));
            Assert.True(sendCalled);
        }

        [Fact]
        public async Task SignOutUserAsync_ShouldUseParentChannel()
        {
            var sendCalled = false;

            MockHttpClient
                .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .Callback<HttpRequestMessage, CancellationToken>((request, ct) =>
                {
                    sendCalled = true;
                    Assert.Contains($"channelId=channel-id", request.RequestUri.ToString());
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(() => client.SignOutUserAsync(UserId, ConnectionName, ChannelId, CancellationToken.None));
            Assert.True(sendCalled);
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldUseParentChannel()
        {
            var sendCalled = false;

            MockHttpClient
                .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .Callback<HttpRequestMessage, CancellationToken>((request, ct) =>
                {
                    sendCalled = true;
                    Assert.Contains($"channelId=channel-id", request.RequestUri.ToString());
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetTokenStatusAsync(UserId, ChannelId, null, CancellationToken.None));
            Assert.True(sendCalled);
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldUseParentChannel()
        {
            var sendCalled = false;

            MockHttpClient
                .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .Callback<HttpRequestMessage, CancellationToken>((request, ct) =>
                {
                    sendCalled = true;
                    Assert.Contains($"channelId=channel-id", request.RequestUri.ToString());
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetAadTokensAsync(UserId, ConnectionName, null, ChannelId, CancellationToken.None));
            Assert.True(sendCalled);
        }

        [Fact]
        public async Task ExchangeTokenAsync_ShouldUseParentChannel()
        {
            var sendCalled = false;

            MockHttpClient
                .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .Callback<HttpRequestMessage, CancellationToken>((request, ct) =>
                {
                    sendCalled = true;
                    Assert.Contains($"channelId=channel-id", request.RequestUri.ToString());
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, new TokenExchangeRequest(), CancellationToken.None));
            Assert.True(sendCalled);
        }

        [Fact]
        public async Task GetTokenOrSignInResourceAsync_ShouldUseParentChannel()
        {
            var sendCalled = false;

            MockHttpClient
                .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .Callback<HttpRequestMessage, CancellationToken>((request, ct) =>
                {
                    sendCalled = true;
                    Assert.Contains($"channelId=channel-id", request.RequestUri.ToString());
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            var client = UseClient();

            await client.GetTokenOrSignInResourceAsync(ConnectionName, new Activity() { From = new ChannelAccount() { Id = UserId }, ChannelId = ChannelId }, null, cancellationToken: CancellationToken.None);
            Assert.True(sendCalled);
        }

        private static RestUserTokenClient UseClient()
        {
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(a => a.CreateClient(It.IsAny<string>()))
                .Returns(MockHttpClient.Object);

            return new RestUserTokenClient("appId", Endpoint, httpFactory.Object, () => Task.FromResult<string>("test"));
        }
    }
}
