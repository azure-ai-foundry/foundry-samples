// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TaskModuleResponseBaseTests
    {
        [Fact]
        public void TaskModuleResponseBaseInits()
        {
            var type = "message";

            var responseBase = new TaskModuleResponseBase(type);

            Assert.NotNull(responseBase);
            Assert.IsType<TaskModuleResponseBase>(responseBase);
            Assert.Equal(type, responseBase.Type);
        }

        [Fact]
        public void TaskModuleResponseBaseInitsWithNoArgs()
        {
            var responseBase = new TaskModuleResponseBase();

            Assert.NotNull(responseBase);
            Assert.IsType<TaskModuleResponseBase>(responseBase);
        }
    }
}
