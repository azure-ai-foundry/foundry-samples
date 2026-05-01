// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TaskModuleRequestContextTests
    {
        [Fact]
        public void TaskModuleRequestContextInits()
        {
            var theme = "chat";

            var requestContext = new TaskModuleRequestContext(theme);

            Assert.NotNull(requestContext);
            Assert.IsType<TaskModuleRequestContext>(requestContext);
            Assert.Equal(theme, requestContext.Theme);
        }

        [Fact]
        public void TaskModuleRequestContextInitsWithNoArgs()
        {
            var requestContext = new TaskModuleRequestContext();

            Assert.NotNull(requestContext);
            Assert.IsType<TaskModuleRequestContext>(requestContext);
        }
    }
}
