// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.State.Tests
{
    public class TestUtilities
    {
        public static ITurnContext CreateEmptyContext()
        {
            var b = new TestAdapter();
            var a = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = "EmptyContext",
                Conversation = new ConversationAccount
                {
                    Id = "test",
                },
                From = new ChannelAccount
                {
                    Id = "empty@empty.context.org",
                },
            };
            var bc = new TurnContext(b, a);

            return bc;
        }
    }
}
