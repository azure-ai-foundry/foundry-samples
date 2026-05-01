// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TaskModuleContinueResponseTests
    {
        [Fact]
        public void TaskModuleContinueResponseInits()
        {
            var value = new TaskModuleTaskInfo();

            var continueResponse = new TaskModuleContinueResponse(value);

            Assert.NotNull(continueResponse);
            Assert.IsType<TaskModuleContinueResponse>(continueResponse);
            Assert.Equal(value, continueResponse.Value);
            Assert.Equal("continue", continueResponse.Type);
        }

        [Fact]
        public void TaskModuleContinueResponseInitsWithNoArgs()
        {
            var continueResponse = new TaskModuleContinueResponse();

            Assert.NotNull(continueResponse);
            Assert.IsType<TaskModuleContinueResponse>(continueResponse);
            Assert.Equal("continue", continueResponse.Type);
        }
    }
}
