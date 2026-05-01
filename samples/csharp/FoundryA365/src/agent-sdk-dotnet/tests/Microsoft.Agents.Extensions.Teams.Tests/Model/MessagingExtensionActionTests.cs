// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel.Design;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
#if !NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
#endif
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class MessagingExtensionActionTests
    {
        [Fact]
        public void MessagingExtensionActionInits()
        {
            var data = new Dictionary<string, string>() { { "key", "value" } };
            var context = new TaskModuleRequestContext("theme");
            var commandId = "commandId";
            var commandContext = "message";
            var botMessagePreviewAction = "send";
            var botActivityPreview = new List<Activity>() { new Activity(text: "hi"), new Activity(text: "yo yo yo") };
            var messagePayload = new MessageActionsPayload("msgId", "1234", "message");
            var state = "secureOAuthState1234";

            var msgExtAction = new MessagingExtensionAction(data, context, commandId, commandContext, botMessagePreviewAction, botActivityPreview, messagePayload)
            {
                State = state
            };

            Assert.NotNull(msgExtAction);
            Assert.IsType<MessagingExtensionAction>(msgExtAction);
            Assert.Equal(data, msgExtAction.Data);
            Assert.Equal(context, msgExtAction.Context);
            Assert.Equal(commandId, msgExtAction.CommandId);
            Assert.Equal(commandContext, msgExtAction.CommandContext);
            Assert.Equal(botMessagePreviewAction, msgExtAction.BotMessagePreviewAction);
            Assert.Equal(botActivityPreview, msgExtAction.BotActivityPreview);
            Assert.Equal(messagePayload, msgExtAction.MessagePayload);
            Assert.Equal(state, msgExtAction.State);
        }

        [Fact]
        public void MessagingExtensionActionInitsNoArgs()
        {
            var msgExtAction = new MessagingExtensionAction();

            Assert.NotNull(msgExtAction);
            Assert.IsType<MessagingExtensionAction>(msgExtAction);
        }

        [Fact]
        public void MessagingExtensionActionRoundTrip()
        {
            var msgExtAction = new MessagingExtensionAction()
            {
                CommandId = "commandId",
                CommandContext = "commandContext",
                BotMessagePreviewAction = "botMessagePreviewAction",
                BotActivityPreview = [
                        new Activity() { Type = ActivityTypes.Message }
                    ],
                MessagePayload = new MessageActionsPayload()
                {
                    Id = "id",
                    ReplyToId = "replyToId",
                    MessageType = "messageType",
                    CreatedDateTime = "2025-06-15T13:45:30",
                    LastModifiedDateTime = "2025-07-17T14:46:40",
                    Deleted = false,
                    Subject = "subject",
                    Summary = "summary",
                    Importance = "importance",
                    Locale = "locale",
                    From = new MessageActionsPayloadFrom()
                    {
                        User = new MessageActionsPayloadUser()
                        {
                            UserIdentityType = "userIdentityType",
                            Id = "id",
                            DisplayName = "displayName"
                        },
                        Application = new MessageActionsPayloadApp()
                        {
                            ApplicationIdentityType = "applicationIdentityType",
                            Id = "id",
                            DisplayName = "displayName"
                        },
                        Conversation = new MessageActionsPayloadConversation()
                        {
                            ConversationIdentityType = "conversationIdentityType",
                            Id = "id",
                            DisplayName = "displayName"
                        }
                    },
                    Body = new MessageActionsPayloadBody()
                    {
                        ContentType = "contentType",
                        Content = "content"
                    },
                    AttachmentLayout = "attachmentLayout",
                    Attachments = [
                            new MessageActionsPayloadAttachment()
                            {
                                Id = "id",
                                ContentType = "contentType",
                                ContentUrl = "contentUrl",
                                Content = "content",
                                Name = "name",
                                ThumbnailUrl = "thumbnailUrl"
                            }
                        ],
                    Mentions = [
                            new MessageActionsPayloadMention()
                            {
                                Id = 1,
                                MentionText = "mentionText",
                                Mentioned = new MessageActionsPayloadFrom()
                            }
                        ],
                    Reactions = [
                            new MessageActionsPayloadReaction()
                            {
                                ReactionType = "reactionType",
                                CreatedDateTime = "createdDateTime",
                                User = new MessageActionsPayloadFrom()
                            }
                        ]
                }

            };

            // Known good
            var goodJson = LoadTestJson.LoadJson(msgExtAction);

            // Out
            var json = ProtocolJsonSerializer.ToJson(msgExtAction);
            Assert.Equal(goodJson, json);

            // In
            var inObj = ProtocolJsonSerializer.ToObject<MessagingExtensionAction>(json);
            json = ProtocolJsonSerializer.ToJson(inObj);
            Assert.Equal(goodJson, json);
        }
    }
}
