// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    [Trait("TestCategory", "Prompts")]
    [Trait("TestCategory", "Choice Tests")]
    public class ChoicesChannelTests
    {
        [Fact]
        public void ShouldReturnTrueForSupportsSuggestedActionsWithLineAnd13()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.Line, 13);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnFalseForSupportsSuggestedActionsWithLineAnd14()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.Line, 14);
            Assert.False(supports);
        }

        [Fact]
        public void ShouldReturnTrueForSupportsSuggestedActionsWithSkypeAnd10()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.Skype, 10);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnFalseForSupportsSuggestedActionsWithSkypeAnd11()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.Skype, 11);
            Assert.False(supports);
        }

        [Fact]
        public void ShouldReturnTrueForSupportsSuggestedActionsWithKikAnd20()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.Kik, 20);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnFalseForSupportsSuggestedActionsWithKikAnd21()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.Skype, 21);
            Assert.False(supports);
        }

        [Fact]
        public void ShouldReturnTrueForSupportsSuggestedActionsWithEmulatorAnd100()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.Emulator, 100);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnFalseForSupportsSuggestedActionsWithEmulatorAnd101()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.Emulator, 101);
            Assert.False(supports);
        }

        [Fact]
        public void ShouldReturnTrueForSupportsSuggestedActionsWithDirectLineSpeechAnd100()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.DirectlineSpeech, 100);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnTrueForSupportsSuggestedActionsWithTeamsAndPersonalAnd3()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.Msteams, 3, ConversationTypes.Personal);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnFalseForSupportsSuggestedActionsWithTeamsAndPersonalAnd4()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.Msteams, 4, ConversationTypes.Personal);
            Assert.False(supports);
        }

        [Fact]
        public void ShouldReturnFalseForSupportsSuggestedActionsWithTeamsAndGroupChat()
        {
            var supports = Channels.SupportsSuggestedActions(Channels.Msteams, 3, ConversationTypes.GroupChat);
            Assert.False(supports);
        }

        [Fact]
        public void ShouldReturnTrueForSupportsCardActionsWithDirectLineSpeechAnd99()
        {
            var supports = Channels.SupportsCardActions(Channels.DirectlineSpeech, 99);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnTrueForSupportsCardActionsWithLineAnd99()
        {
            var supports = Channels.SupportsCardActions(Channels.Line, 99);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnFalseForSupportsCardActionsWithLineAnd100()
        {
            var supports = Channels.SupportsCardActions(Channels.Line, 100);
            Assert.False(supports);
        }

        [Fact]
        public void ShouldReturnTrueForSupportsCardActionsWithCortanaAnd100()
        {
            var supports = Channels.SupportsCardActions(Channels.Cortana, 100);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnTrueForSupportsCardActionsWithSlackAnd100()
        {
            var supports = Channels.SupportsCardActions(Channels.Slack, 100);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnTrueForSupportsCardActionsWithSkypeAnd100()
        {
            var supports = Channels.SupportsCardActions(Channels.Skype, 3);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnFalseForSupportsCardActionsWithSkypeAnd5()
        {
            var supports = Channels.SupportsCardActions(Channels.Skype, 5);
            Assert.False(supports);
        }
        
        [Fact]
        public void ShouldReturnTrueForSupportsCardActionsWithTeamsAnd50()
        {
            var supports = Channels.SupportsCardActions(Channels.Msteams, 50);
            Assert.True(supports);
        }

        [Fact]
        public void ShouldReturnFalseForSupportsCardActionsWithTeamsAnd51()
        {
            var supports = Channels.SupportsCardActions(Channels.Msteams, 51);
            Assert.False(supports);
        }

        [Fact]
        public void ShouldReturnFalseForHasMessageFeedWithCortana()
        {
            var supports = Channels.HasMessageFeed(Channels.Cortana);
            Assert.False(supports);
        }
    }
}
