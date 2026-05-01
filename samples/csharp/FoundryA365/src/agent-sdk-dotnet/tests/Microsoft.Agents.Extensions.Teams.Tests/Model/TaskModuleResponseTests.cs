// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TaskModuleResponseTests
    {
        [Fact]
        public void TaskModuleResponseInits()
        {
            var task = new TaskModuleResponseBase();
            var cacheInfo = new CacheInfo();

            var response = new TaskModuleResponse(task)
            {
                CacheInfo = cacheInfo
            };

            Assert.NotNull(response);
            Assert.IsType<TaskModuleResponse>(response);
            Assert.Equal(task, response.Task);
            Assert.Equal(cacheInfo, response.CacheInfo);
        }

        [Fact]
        public void TaskModuleResponseInitsWithNoArgs()
        {
            var response = new TaskModuleResponse();

            Assert.NotNull(response);
            Assert.IsType<TaskModuleResponse>(response);
        }

        [Fact]
        public void TaskModuleResponseRoundTrip()
        {
            var response = new TaskModuleResponse()
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
            var secondResponse = ProtocolJsonSerializer.ToObject<TaskModuleResponse>(json);

            Assert.NotNull(secondResponse);
            Assert.NotNull(secondResponse.Task);

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
