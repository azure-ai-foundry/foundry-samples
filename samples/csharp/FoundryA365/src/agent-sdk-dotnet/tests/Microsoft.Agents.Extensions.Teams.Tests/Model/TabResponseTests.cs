// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;
using static Microsoft.Agents.Extensions.Teams.Tests.Model.TabsTestData;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TabResponseTests
    {
        [Theory]
        [ClassData(typeof(TabResponseTestData))]
        public void TabResponseInits(TabResponsePayload tab)
        {
            var tabResponse = new TabResponse()
            {
                Tab = tab
            };

            Assert.NotNull(tabResponse);
            Assert.IsType<TabResponse>(tabResponse);
            Assert.Equal(tab, tabResponse.Tab);
        }
    }
}
