// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;
using static Microsoft.Agents.Extensions.Teams.Tests.Model.TabsTestData;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TabSuggestedActionsTests
    {
        [Theory]
        [ClassData(typeof(TabSuggestedActionsTestData))]
        public void TabSuggestedActionsInits(IList<CardAction> actions)
        {
            var suggestedActions = new TabSuggestedActions()
            {
                Actions = actions
            };

            Assert.NotNull(suggestedActions);
            Assert.IsType<TabSuggestedActions>(suggestedActions);
            Assert.Equal(actions, suggestedActions.Actions);
        }
    }
}
