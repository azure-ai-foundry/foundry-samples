// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;
using static Microsoft.Agents.Extensions.Teams.Tests.Model.TabsTestData;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TabResponseCardsTests
    {
        [Theory]
        [ClassData(typeof(TabResponseCardsTestData))]
        public void TabResponseCardsInits(IList<TabResponseCard> cards)
        {
            var responseCards = new TabResponseCards()
            {
                Cards = cards
            };

            Assert.NotNull(responseCards);
            Assert.IsType<TabResponseCards>(responseCards);
            Assert.Equal(cards, responseCards.Cards);
        }
    }
}
