// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TaskModuleTaskInfoTests
    {
        [Fact]
        public void TaskModuleTaskInfoInits()
        {
            var title = "chatty";
            var height = "medium";
            var width = "large";
            var url = "https://example.com";
            var card = new Attachment();
            var fallbackUrl = "https://fallback-url-of-example.com";
            var completionBotId = "0000-0000-0000-0000-0000";

            var taskInfo = new TaskModuleTaskInfo(title, height, width, url, card, fallbackUrl, completionBotId);

            Assert.NotNull(taskInfo);
            Assert.IsType<TaskModuleTaskInfo>(taskInfo);
            Assert.Equal(title, taskInfo.Title);
            Assert.Equal(height, taskInfo.Height);
            Assert.Equal(width, taskInfo.Width);
            Assert.Equal(url, taskInfo.Url);
            Assert.Equal(card, taskInfo.Card);
            Assert.Equal(fallbackUrl, taskInfo.FallbackUrl);
            Assert.Equal(completionBotId, taskInfo.CompletionBotId);
        }

        [Fact]
        public void TaskModuleTaskInfoInitsWithNoArgs()
        {
            var taskInfo = new TaskModuleTaskInfo();

            Assert.NotNull(taskInfo);
            Assert.IsType<TaskModuleTaskInfo>(taskInfo);
        }

        [Fact]
        public void TaskModuleTaskMessagingExtensionResponse()
        {
            var response = new MessagingExtensionActionResponse()
            {
                Task = new TaskModuleContinueResponse()
                {
                    Value = new TaskModuleTaskInfo()
                    {
                        Height = 200,
                        Width = 400,
                        Title = "Test",
                        Url = "TestUrl",
                    }
                }
            };

            var json = ProtocolJsonSerializer.ToJson(response);
            var secondResponse = ProtocolJsonSerializer.ToObject<MessagingExtensionActionResponse>(json);

            // this is to verify the roundtrip would contain Properties the subclass of TaskModuleResponseBase
            // contained.  Since TaskModuleResponseBase is not usually deserialized, the converter doesn't
            // create the original type.  If could if there is discriminating values to do so, which would 
            // be tricky.
            var task = secondResponse.Task;
            Assert.IsAssignableFrom<TaskModuleResponseBase>(task);
            Assert.NotEmpty(task.Properties);
            Assert.True(task.Properties.ContainsKey("value"));

            // Would need a converter for TaskModuleContinueResponse to get the actual type.  Otherwise use
            // ToObject.
            var valueElements = task.Properties["value"].ToJsonElements();
            Assert.True(valueElements.ContainsKey("height"));
            Assert.True(valueElements.ContainsKey("width"));
            Assert.True(valueElements.ContainsKey("title"));
            Assert.True(valueElements.ContainsKey("url"));
        }
    }
}
