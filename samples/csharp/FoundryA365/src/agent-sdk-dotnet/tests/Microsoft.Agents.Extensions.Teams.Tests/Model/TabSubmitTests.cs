// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;
using static Microsoft.Agents.Extensions.Teams.Tests.Model.TabsTestData;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TabSubmitTests
    {
        [Theory]
        [ClassData(typeof(TabSubmitTestData))]
        public void TabSubmitInits(TabEntityContext tabEntityContext, TabContext tabContext, TabSubmitData tabSubmitData)
        {
            var submit = new TabSubmit()
            {
                TabEntityContext = tabEntityContext,
                Context = tabContext,
                Data = tabSubmitData,
            };

            Assert.NotNull(submit);
            Assert.IsType<TabSubmit>(submit);
            Assert.Equal(tabEntityContext, submit.TabEntityContext);
            Assert.Equal(tabContext, submit.Context);
            Assert.Equal(tabSubmitData, submit.Data);
        }
    }
}
