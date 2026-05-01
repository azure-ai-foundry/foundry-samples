// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Agents.Builder.Testing
{
    public class AgentTestHostTests
    {
        [Fact]
        public void Create_ReturnsHostWithAdapter()
        {
            using var host = AgentTestHost.Create(builder =>
            {
                builder.Services.AddTransient<IAgent, EchoAgent>();
            });

            Assert.NotNull(host.Adapter);
        }

        [Fact]
        public void Adapter_HasDefaultConversation()
        {
            using var host = AgentTestHost.Create(builder =>
            {
                builder.Services.AddTransient<IAgent, EchoAgent>();
            });

            Assert.Equal(Channels.Test, host.Adapter.Conversation.ChannelId);
            Assert.Equal("user1", host.Adapter.Conversation.User.Id);
            Assert.Equal("bot", host.Adapter.Conversation.Agent.Id);
            Assert.Equal("convo1", host.Adapter.Conversation.Conversation.Id);
            Assert.Equal("Conversation1", host.Adapter.Conversation.Conversation.Name);
        }

        [Fact]
        public async Task CreateTestFlow_RunsAgent()
        {
            await using var host = AgentTestHost.Create(builder =>
            {
                builder.Services.AddTransient<IAgent, EchoAgent>();
            });

            await host.CreateTestFlow()
                .Send("hello")
                .AssertReplyContains("Echo: hello")
                .StartTestAsync();
        }

        [Fact]
        public async Task CreateTestFlow_CalledTwice_SameAdapter()
        {
            await using var host = AgentTestHost.Create(builder =>
            {
                builder.Services.AddTransient<IAgent, EchoAgent>();
            });

            // First flow: send and consume reply
            await host.CreateTestFlow()
                .Send("first")
                .AssertReplyContains("Echo: first")
                .StartTestAsync();

            // Second flow on same adapter — queue should be empty after first flow consumed its reply
            await host.CreateTestFlow()
                .Send("second")
                .AssertReplyContains("Echo: second")
                .StartTestAsync();
        }

        [Fact]
        public async Task Adapter_CanSetConversationBeforeCreateTestFlow()
        {
            await using var host = AgentTestHost.Create(builder =>
            {
                builder.Services.AddTransient<IAgent, EchoAgent>();
            });

            host.Adapter.Conversation = new ConversationReference
            {
                ChannelId = "msteams",
                User = new ChannelAccount("custom-user", "Custom"),
                Agent = new ChannelAccount("custom-bot", "Bot"),
                Conversation = new ConversationAccount(false, "custom-conv", "Custom Conv"),
                ServiceUrl = "https://test.com"
            };

            await host.CreateTestFlow()
                .Send("hi")
                .AssertReplyContains("Echo: hi")
                .StartTestAsync();

            Assert.Equal("msteams", host.Adapter.Conversation.ChannelId);
        }

        [Fact]
        public void Create_NullConfigure_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => AgentTestHost.Create(null));
        }

        [Fact]
        public void CreateTestFlow_NoAgentRegistered_Throws()
        {
            using var host = AgentTestHost.Create(_ => { /* IAgent not registered */ });
            Assert.Throws<InvalidOperationException>(() => host.CreateTestFlow());
        }

        // ─── Helper agent ───────────────────────────────────────────────────

        private class EchoAgent : IAgent
        {
            public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
            {
                if (turnContext.Activity.Type == ActivityTypes.Message)
                {
                    await turnContext.SendActivityAsync(
                        $"Echo: {turnContext.Activity.Text}",
                        cancellationToken: cancellationToken);
                }
            }
        }
    }
}
