// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.State.Tests
{
    public class AutoSaveStateMiddlewareTests
    {
        [Fact]
        public async Task AutoSaveStateMiddleware_DualReadWrite()
        {
            var storage = new MemoryStorage();
            var userState = new UserState(storage);
            var convState = new ConversationState(storage);

            var adapter = new TestAdapter(TestAdapter.CreateConversation("AutoSaveStateMiddleware_DualReadWrite"))
                .Use(new AutoSaveStateMiddleware(true, userState, convState));

            const int USER_INITITAL_COUNT = 100;
            const int CONVERSATION_INITIAL_COUNT = 10;
            AgentCallbackHandler botLogic = async (context, cancellationToken) =>
            {
                // get userCount and convCount from hSet
                var userCount = userState.GetValue("userCount", () => USER_INITITAL_COUNT);
                var convCount = convState.GetValue("convCount", () => CONVERSATION_INITIAL_COUNT);

                // System.Diagnostics.Debug.WriteLine($"{context.Activity.Id} UserCount({context.Activity.From.Id}):{userCount} convCount({context.Activity.Conversation.Id}):{convCount}");
                if (context.Activity.Type == ActivityTypes.Message)
                {
                    if (context.Activity.Text == "get userCount")
                    {
                        await context.SendActivityAsync(context.Activity.CreateReply($"{userCount}"), cancellationToken);
                    }
                    else if (context.Activity.Text == "get convCount")
                    {
                        await context.SendActivityAsync(context.Activity.CreateReply($"{convCount}"), cancellationToken);
                    }
                }

                // increment userCount and set property using accessor.  To be saved later by AutoSaveStateMiddleware
                userCount++;
                userState.SetValue("userCount", userCount);

                // increment convCount and set property using accessor.  To be saved later by AutoSaveStateMiddleware
                convCount++;
                convState.SetValue("convCount", convCount);
            };

            await new TestFlow(adapter, botLogic)
               .Send("test1")
                .Send("get userCount")
                    .AssertReply((USER_INITITAL_COUNT + 1).ToString())
                .Send("get userCount")
                    .AssertReply((USER_INITITAL_COUNT + 2).ToString())
                .Send("get convCount")
                    .AssertReply((CONVERSATION_INITIAL_COUNT + 3).ToString())
                .StartTestAsync();


            // Reuse storage with a different conversation
            userState = new UserState(storage);
            convState = new ConversationState(storage);

            // new adapter on new conversation
            adapter = new TestAdapter(new ConversationReference
            {
                ChannelId = "test",
                ServiceUrl = "https://test.com",
                User = new ChannelAccount("user1", "User1"),
                Agent = new ChannelAccount("bot", "Bot"),
                Conversation = new ConversationAccount(false, "convo2", "Conversation2"),
            })
                .Use(new AutoSaveStateMiddleware(true, userState, convState));

            await new TestFlow(adapter, botLogic)
                .Send("get userCount")
                    .AssertReply((USER_INITITAL_COUNT + 4).ToString(), "user count should continue on new conversation")
                .Send("get convCount")
                    .AssertReply((CONVERSATION_INITIAL_COUNT + 1).ToString(), "conversationCount for conversation2 should be reset")
                .StartTestAsync();
        }

        [Fact]
        public async Task AutoSaveStateMiddleware_Chain()
        {
            // setup state
            var storage = new MemoryStorage();
            var convState = new ConversationState(storage);
            var userState = new UserState(storage);

            var bss = new AutoSaveStateMiddleware(true, convState, userState);
            var adapter = new TestAdapter(TestAdapter.CreateConversation("AutoSaveStateMiddleware_Chain"))
                .Use(bss);

            const int USER_INITITAL_COUNT = 100;
            const int CONVERSATION_INITIAL_COUNT = 10;
            AgentCallbackHandler botLogic = async (context, cancellationToken) =>
            {
                // get userCount and convCount from botStateSet
                var userCount = userState.GetValue("userCount", () => USER_INITITAL_COUNT);
                var convCount = convState.GetValue("convCount", () => CONVERSATION_INITIAL_COUNT);

                if (context.Activity.Type == ActivityTypes.Message)
                {
                    if (context.Activity.Text == "get userCount")
                    {
                        await context.SendActivityAsync(context.Activity.CreateReply($"{userCount}"), cancellationToken);
                    }
                    else if (context.Activity.Text == "get convCount")
                    {
                        await context.SendActivityAsync(context.Activity.CreateReply($"{convCount}"), cancellationToken);
                    }
                }

                // increment userCount and set property using accessor.  To be saved later by AutoSaveStateMiddleware
                userCount++;
                userState.SetValue("userCount", userCount);

                // increment convCount and set property using accessor.  To be saved later by AutoSaveStateMiddleware
                convCount++;
                convState.SetValue("convCount", convCount);
            };

            await new TestFlow(adapter, botLogic)
                .Send("test1")
                .Send("get userCount")
                    .AssertReply((USER_INITITAL_COUNT + 1).ToString())
                .Send("get userCount")
                    .AssertReply((USER_INITITAL_COUNT + 2).ToString())
                .Send("get convCount")
                    .AssertReply((CONVERSATION_INITIAL_COUNT + 3).ToString())
                .StartTestAsync();

            // new adapter on new conversation
            // Reuse storage with a different conversation
            userState = new UserState(storage);
            convState = new ConversationState(storage);
            bss = new AutoSaveStateMiddleware(true, convState, userState);

            adapter = new TestAdapter(new ConversationReference
            {
                ChannelId = "test",
                ServiceUrl = "https://test.com",
                User = new ChannelAccount("user1", "User1"),
                Agent = new ChannelAccount("bot", "Bot"),
                Conversation = new ConversationAccount(false, "convo2", "Conversation2"),
            })
                .Use(bss);

            await new TestFlow(adapter, botLogic)
                .Send("get userCount")
                    .AssertReply((USER_INITITAL_COUNT + 4).ToString(), "user count should continue on new conversation")
                .Send("get convCount")
                    .AssertReply((CONVERSATION_INITIAL_COUNT + 1).ToString(), "conversationCount for conversation2 should be reset")
                .StartTestAsync();
        }
    }
}
