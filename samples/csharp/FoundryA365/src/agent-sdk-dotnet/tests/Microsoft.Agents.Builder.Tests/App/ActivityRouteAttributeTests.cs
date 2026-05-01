// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ActivityRouteAttributeTests
    {
        [Fact]
        public async Task ActivityRouteAttribute_Type()
        {
            var app = new TestApp(new AgentApplicationOptions((IStorage) null));
            var turnContext = new Mock<ITurnContext>();
            turnContext
                .Setup(c => c.Activity)
                .Returns(new Activity() { Type = ActivityTypes.Message });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            // Only one route is executed by Application.  In this test, in definition order.
            Assert.Single(app.calls);
            Assert.Equal("OnMessageAsync", app.calls[0]);
        }

        [Fact]
        public async Task ActivityRouteAttribute_Regex()
        {
            var app = new TestApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext
                .Setup(c => c.Activity)
                .Returns(new Activity() { Type = "testActivity" });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            // Only one route is executed by Application.  In this test, in definition order.
            Assert.Single(app.calls);
            Assert.Equal("OnRegExAsync", app.calls[0]);
        }

        [Fact]
        public async Task ActivityRouteAttribute_Selector()
        {
            var app = new TestApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext
                .Setup(c => c.Activity)
                .Returns(new Activity() { Type = ActivityTypes.Message, Text = "test_selector" });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            // Only one route is executed by Application.  In this test, in definition order.
            Assert.Single(app.calls);
            Assert.Equal("OnSelectorAsync", app.calls[0]);
        }

        [Fact]
        public void ActivityRouteAttribute_SelectorNotFound()
        {
            Assert.Throws<ArgumentException>(() => new SelectorNotFoundTestApp(new AgentApplicationOptions((IStorage)null)));
        }

        [Fact]
        public void ActivityRouteAttribute_SelectorInvalid()
        {
            Assert.Throws<ArgumentException>(() => new InvalidSelectorTestApp(new AgentApplicationOptions((IStorage)null)));
        }
    }

    class TestApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [Route(RouteType = RouteType.Activity, Type = ActivityTypes.Message, Rank = RouteRank.Last)]
        public Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add("OnMessageAsync");
            return Task.CompletedTask;
        }

        [Route(RouteType = RouteType.Activity, Type = ActivityTypes.Message, Rank = RouteRank.Last)]
        public Task OnMessageDuplicateAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add("OnMessageDuplicateAsync");
            return Task.CompletedTask;
        }

        protected Task<bool> TestSelectorAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(turnContext.Activity.Text == "test_selector");
        }

        [Route(Selector = "TestSelectorAsync")]
        public Task OnSelectorAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add("OnSelectorAsync");
            return Task.CompletedTask;
        }

        [Route(Regex = "test*.")]
        public Task OnRegExAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add("OnRegExAsync");
            return Task.CompletedTask;
        }
    }

    class SelectorNotFoundTestApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        [Route(Selector = "NotFoundSelectorAsync")]
        public Task OnNotFoundSelectorAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    class InvalidSelectorTestApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        // incorrect RouteSelectorAsync signature
        protected Task<int> InvalidSelectorAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(1);
        }

        [Route(Selector = "InvalidSelectorAsync")]
        public Task OnInvalidSelectorAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
