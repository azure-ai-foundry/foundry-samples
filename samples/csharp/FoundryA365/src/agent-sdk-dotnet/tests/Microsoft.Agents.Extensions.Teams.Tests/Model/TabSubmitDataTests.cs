// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using static Microsoft.Agents.Extensions.Teams.Tests.Model.TabsTestData;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TabSubmitDataTests
    {
        [Theory]
        [ClassData(typeof(TabSubmitDataTestData))]
        public void TabSubmitDataInits(string tabType, IDictionary<string, JsonElement> properties)
        {
            var submitData = new TabSubmitData()
            {
                Type = tabType,
                Properties = properties
            };

            Assert.NotNull(submitData);
            Assert.IsType<TabSubmitData>(submitData);
            Assert.Equal(tabType, submitData.Type);

            var dataProps = submitData.Properties;
            Assert.Equal(properties, dataProps);
            if (dataProps != null)
            {
                Assert.Equal(properties.Count, submitData.Properties.Count);
            }
        }
    }
}
