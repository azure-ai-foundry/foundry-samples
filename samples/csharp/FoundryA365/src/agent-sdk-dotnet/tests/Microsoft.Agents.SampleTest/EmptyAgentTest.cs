// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using EmptyAgent;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Transcript;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest
{
    /// <summary>
    /// Demonstrates how to test a simple agent using <see cref="AgentTestHost"/> and
    /// <see cref="TestFlow"/> from <c>Microsoft.Agents.Builder.Testing</c>.
    ///
    /// <para>
    /// <see cref="AgentTestHost"/> spins up a lightweight DI host that pre-wires a
    /// <see cref="TestAdapter"/> as the channel adapter, so tests exercise real agent
    /// routing logic without an HTTP server or Azure Bot Service connection.
    /// </para>
    /// </summary>
    public class EmptyAgentTest
    {
        /// <summary>
        /// Verifies the full greeting + echo conversation for <c>MyAgent</c>:
        /// <list type="number">
        ///   <item>A ConversationUpdate with the user as a member-added triggers "Hello and Welcome!"</item>
        ///   <item>A plain text message is echoed back as "You said: {text}"</item>
        ///   <item>No further replies are sent after the echo.</item>
        /// </list>
        ///
        /// <para>
        /// The test also attaches <see cref="TranscriptLoggerMiddleware"/> to the adapter before
        /// creating the flow, demonstrating how to capture a full conversation transcript for
        /// post-hoc assertions.
        /// </para>
        /// </summary>
        [Fact]
        public async Task Test_EmptyAgentEcho()
        {
            // --- Arrange -----------------------------------------------------------------

            // AgentTestHost.Create wires up a real DI container with TestAdapter pre-registered
            // as IChannelAdapter. Register IAgent directly — do NOT use AddAgent<T>() here
            // because that also registers CloudAdapter, which conflicts with TestAdapter.
            await using var host = AgentTestHost.Create(builder =>
            {
                builder.Services.AddSingleton<IStorage, MemoryStorage>();

                // Factory registration is used instead of AddTransient<IAgent, MyAgent>() because
                // AgentApplicationOptions must be constructed with an IStorage instance resolved
                // from DI. If your agent has a fully DI-injected constructor, you can use
                // builder.Services.AddTransient<IAgent, MyAgent>() instead.
                builder.Services.AddTransient<IAgent>(sp =>
                    new MyAgent(new AgentApplicationOptions(sp.GetRequiredService<IStorage>())));
            });

            // Attach transcript middleware to the shared adapter before creating any TestFlow.
            // This records every inbound and outbound activity for inspection after the flow runs.
            var transcript = new MemoryTranscriptStore();
            host.Adapter.Use(new TranscriptLoggerMiddleware(transcript));

            // --- Act ---------------------------------------------------------------------

            await host.CreateTestFlow()
                // SendConversationUpdate creates a ConversationUpdate activity with the
                // specified members added. MyAgent greets every member whose Id differs
                // from the Recipient (the agent itself).
                .SendConversationUpdate(new[] { new ChannelAccount { Id = host.Adapter.Conversation.User.Id } })
                .AssertReply("Hello and Welcome!")

                // Send a plain message and assert the echo reply using AssertReplySatisfies,
                // which accepts an async delegate so you can run any xUnit assertions on the
                // full IActivity — not just the text.
                .Send("hello")
                .AssertReplySatisfies(reply =>
                {
                    Assert.Equal("You said: hello", reply.Text);
                    Assert.Equal(ActivityTypes.Message, reply.Type);
                    return Task.CompletedTask;
                })

                // Assert that the agent sends no further replies within the default 1-second
                // window. This guards against accidental extra sends in future refactors.
                .AssertNoMoreReplies()

                .StartTestAsync();

            // --- Assert (transcript) -----------------------------------------------------

            // Verify the transcript captured the expected number of activities:
            //   1. Inbound ConversationUpdate
            //   2. Outbound "Hello and Welcome!"
            //   3. Inbound "hello" message
            //   4. Outbound "You said: hello"
            //
            // NOTE: TranscriptLoggerMiddleware flushes activities fire-and-forget. This
            // assertion is reliable here because MemoryTranscriptStore.LogActivityAsync is
            // synchronous, so the flush completes before the thread pool is rescheduled.
            // If you swap in an async store (BlobsTranscriptStore, etc.) you would need a
            // small delay or poll-with-timeout before reading the transcript.
            var transcriptActivities = await GetTranscriptAsync(
                host.Adapter.Conversation.ChannelId,
                host.Adapter.Conversation.Conversation.Id,
                transcript);

            Assert.Equal(4, transcriptActivities.Count);
        }

        // ---------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Reads all activities from a <see cref="ITranscriptStore"/>, following
        /// continuation tokens until the full page set is exhausted.
        /// </summary>
        private static async Task<IList<IActivity>> GetTranscriptAsync(
            string channelId,
            string conversationId,
            ITranscriptStore store)
        {
            var activities = new List<IActivity>();

            string continuationToken = null;
            do
            {
                var pagedResult = await store.GetTranscriptActivitiesAsync(channelId, conversationId, continuationToken);
                continuationToken = pagedResult.ContinuationToken;
                activities.AddRange(pagedResult.Items);
            } while (continuationToken != null);

            return activities;
        }
    }
}
