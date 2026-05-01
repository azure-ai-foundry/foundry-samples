// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TaskModuleActionTests
    {
        [Theory]
        [InlineData("NullValueButton", null)]
        [InlineData("StringValueButton", "{}")]
        [InlineData("JObjectValueButton", "makeJObject")]
        public void TaskModuleActionInits(string title, object value)
        {
            if ((string)value == "makeJObject")
            {
                value = JsonSerializer.SerializeToElement(new { key = "value" });
            }

            var action = new TaskModuleAction(title, value);
            var expectedKey = "type";
            var expectedVal = "task/fetch";

            Assert.NotNull(action);
            Assert.IsType<TaskModuleAction>(action);
            Assert.Equal(title, action.Title);
            var valAsObj = action.Value.ToJsonElements();
            Assert.True(valAsObj.ContainsKey(expectedKey));
            Assert.Equal(expectedVal, valAsObj[expectedKey].ToString());
        }
    }
}
