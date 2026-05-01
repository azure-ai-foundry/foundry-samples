// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Xunit;
using Microsoft.Agents.Extensions.SharePoint.Models;
using Microsoft.Agents.Extensions.SharePoint;
using Microsoft.Agents.Extensions.SharePoint.Compat;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Testing;

namespace Microsoft.Agents.Extensions.SharePoint.Tests
{
    public class SharePointActivityHandlerTests
    {
        [Fact]
        public async Task TestSharePointGetCardViewAction()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/getCardView",
                Value = new JsonObject()
            };

            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSharePointTaskGetCardViewAsync", bot.Record[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.IsType<InvokeResponse>(activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestSharePointGetQuickViewAction()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/getQuickView",
                Value = new JsonObject()
            };

            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSharePointTaskGetQuickViewAsync", bot.Record[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.IsType<InvokeResponse>(activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestSharePointGetPropertyPaneConfigurationAction()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/getPropertyPaneConfiguration",
                Value = new JsonObject()
            };

            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSharePointTaskGetPropertyPaneConfigurationAsync", bot.Record[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.IsType<InvokeResponse>(activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestSharePointSetPropertyPaneConfigurationAction()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/setPropertyPaneConfiguration",
                Value = new JsonObject()
            };

            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSharePointTaskSetPropertyPaneConfigurationAsync", bot.Record[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.IsType<InvokeResponse>(activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestSharePointHandleActionAction()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "cardExtension/handleAction",
                Value = new JsonObject()
            };

            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnSharePointTaskHandleActionAsync", bot.Record[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.IsType<InvokeResponse>(activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)activitiesToSend[0].Value).Status);
        }

        private class TestActivityHandler : SharePointActivityHandler
        {
            public List<string> Record { get; } = new List<string>();

            // Invoke
            protected override Task<CardViewResponse> OnSharePointTaskGetCardViewAsync(ITurnContext<IInvokeActivity> turnContext, AceRequest aceRequest, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult(new CardViewResponse());
            }

            protected override Task<GetPropertyPaneConfigurationResponse> OnSharePointTaskGetPropertyPaneConfigurationAsync(ITurnContext<IInvokeActivity> turnContext, AceRequest aceRequest, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult(new GetPropertyPaneConfigurationResponse());
            }

            protected override Task<QuickViewResponse> OnSharePointTaskGetQuickViewAsync(ITurnContext<IInvokeActivity> turnContext, AceRequest aceRequest, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult(new QuickViewResponse());
            }

            protected override Task<BaseHandleActionResponse> OnSharePointTaskSetPropertyPaneConfigurationAsync(ITurnContext<IInvokeActivity> turnContext, AceRequest aceRequest, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult<BaseHandleActionResponse>(new NoOpHandleActionResponse());
            }

            protected override Task<BaseHandleActionResponse> OnSharePointTaskHandleActionAsync(ITurnContext<IInvokeActivity> turnContext, AceRequest aceRequest, CancellationToken cancellationToken)
            {
                Record.Add(MethodBase.GetCurrentMethod().Name);
                return Task.FromResult<BaseHandleActionResponse>(new NoOpHandleActionResponse());
            }
        }
    }
}
