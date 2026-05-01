// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class MessagingExtensionSuggestedActionTests
    {
        [Fact]
        public void MessagingExtensionSuggestedActionInits()
        {
            var cardActions = new List<CardAction>()
            {
                new CardAction("openUrl"),
                new CardAction("imBack"),
                new CardAction("postBack"),
            };

            var msgExtSuggestedAction = new MessagingExtensionSuggestedAction(cardActions);

            Assert.NotNull(msgExtSuggestedAction);
            Assert.IsType<MessagingExtensionSuggestedAction>(msgExtSuggestedAction);
            Assert.Equal(cardActions, msgExtSuggestedAction.Actions);
            Assert.Equal(cardActions.Count, msgExtSuggestedAction.Actions.Count);
        }

        [Fact]
        public void MessagingExtensionSuggestedActionInitsWithNoArgs()
        {
            var msgExtSuggestedAction = new MessagingExtensionSuggestedAction();

            Assert.NotNull(msgExtSuggestedAction);
            Assert.IsType<MessagingExtensionSuggestedAction>(msgExtSuggestedAction);
        }
    }
}
