// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Testing
{
    /// <summary>
    /// A thin test harness that configures an agent using the same DI extension-method style
    /// as production <c>Program.cs</c>, then produces ready-to-use <see cref="TestFlow"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>Pre-registers <see cref="TestAdapter"/> as <see cref="IChannelAdapter"/> singleton.</para>
    /// <para>Use <c>await using var host = AgentTestHost.Create(...);</c> in async tests (preferred),
    /// or <c>using var host = AgentTestHost.Create(...);</c> in sync contexts.</para>
    /// <para>Register your agent directly as <see cref="IAgent"/> — do not use <c>AddAgent&lt;T&gt;()</c>,
    /// which also registers <c>CloudAdapter</c> and conflicts with the pre-registered <see cref="TestAdapter"/>.</para>
    /// </remarks>
    public sealed class AgentTestHost : IDisposable, IAsyncDisposable
    {
        private readonly IHost _host;

        private AgentTestHost(IHost host, TestAdapter adapter)
        {
            _host = host;
            Adapter = adapter;
        }

        /// <summary>
        /// Gets the pre-configured <see cref="TestAdapter"/> shared by all test flows created from this host.
        /// </summary>
        public TestAdapter Adapter { get; }

        /// <summary>
        /// Creates and starts a new <see cref="AgentTestHost"/>.
        /// </summary>
        /// <param name="configure">
        /// Callback to configure services and options — same style as production <c>Program.cs</c>.
        /// At minimum, register <c>IAgent</c> as transient:
        /// <code>builder.Services.AddTransient&lt;IAgent, MyAgent&gt;();</code>
        /// Do not use <c>AddScoped</c> for <c>IAgent</c> or its dependencies; the host resolves
        /// agents from the root scope, and scoped services captured there will not be disposed
        /// between test turns.
        /// </param>
        /// <returns>A started <see cref="AgentTestHost"/>. Dispose when the test is done.</returns>
        public static AgentTestHost Create(Action<IHostApplicationBuilder> configure)
        {
            AssertionHelpers.ThrowIfNull(configure, nameof(configure));

            var builder = Host.CreateApplicationBuilder();

            // Pre-register TestAdapter as IChannelAdapter singleton
            var adapter = new TestAdapter();
            builder.Services.AddSingleton(adapter);
            builder.Services.AddSingleton<IChannelAdapter>(adapter);

            configure(builder);

            var host = builder.Build();
            // Blocking call is intentional: Create() is a synchronous factory used in both sync
            // and async test contexts. IHost.StartAsync uses ConfigureAwait(false) internally,
            // so this does not deadlock on the xUnit test thread. Prefer DisposeAsync for teardown.
            host.StartAsync().GetAwaiter().GetResult();

            return new AgentTestHost(host, adapter);
        }

        /// <summary>
        /// Creates a <see cref="TestFlow"/> wired to the <see cref="IAgent"/> resolved from DI.
        /// Because <see cref="IAgent"/> is registered as transient, each call produces a new agent instance.
        /// All flows share the same <see cref="Adapter"/> and its <see cref="TestAdapter.ActiveQueue"/>.
        /// </summary>
        /// <remarks>
        /// <para>The agent is resolved from the root DI scope. Register <see cref="IAgent"/> and its
        /// dependencies as transient or singleton — scoped registrations will behave as singletons
        /// for the lifetime of the host.</para>
        /// </remarks>
        public TestFlow CreateTestFlow()
        {
            var agent = _host.Services.GetRequiredService<IAgent>();
            return new TestFlow(Adapter, agent);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Blocking stop is intentional for sync using-statement disposal in tests.
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync().ConfigureAwait(false);
            _host.Dispose();
        }
    }
}
