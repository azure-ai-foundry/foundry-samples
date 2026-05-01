// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System.Reflection;
using System;
using Xunit;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;

namespace Microsoft.Agents.Client.Tests
{
    public class ManifestGenerateTests
    {
        [Fact(Skip = "Test manifest gen from AgentApplication")]
        public void Test_ManifestGenerate()
        {
            var activities = new Dictionary<string, ActivityInfo>();

            var app = new ManifestAgent(new AgentApplicationOptions((IStorage) null));

            foreach (var method in app.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var activityRoutes = method.GetCustomAttributes<Attribute>(true);
                foreach (var attribute in activityRoutes)
                {
                    // Add route for all IRouteAttribute instances
                    if (attribute is RouteAttribute routeAttribute)
                    {
                        if (IsActivityType(routeAttribute))
                        {
                            var methodName = method.Name.Replace("On", "").Replace("Async", "");
                            var camelName = Char.ToLowerInvariant(methodName[0]) + methodName.Substring(1);

                            string type = null;
                            if (routeAttribute.RouteType == RouteType.Activity)
                                type = routeAttribute.Type;
                            else if (routeAttribute.RouteType == RouteType.Event)
                                type = ActivityTypes.Event;
                            else if (routeAttribute.RouteType == RouteType.Conversation)
                                type = ActivityTypes.ConversationUpdate;

                            activities.Add(camelName, new ActivityInfo() { Name = methodName, Type = type });
                        }
                    }
                }
            }

            var json = "{\"activities\":" + ProtocolJsonSerializer.ToJson(activities) + "}";

            var expected = "{\"activities\":{\"message\":{\"name\":\"Message\",\"type\":\"message\"},\"test\":{\"name\":\"Test\",\"type\":\"event\"}}}";
            Assert.Equal(expected, json);
        }

        private bool IsActivityType(RouteAttribute routeAttribute)
        {
            return routeAttribute.RouteType == RouteType.Activity
                || routeAttribute.RouteType == RouteType.Message
                || routeAttribute.RouteType == RouteType.Conversation
                || routeAttribute.RouteType == RouteType.Event;
        }
    }

    class ManifestAgent : AgentApplication
    {
        public ManifestAgent(AgentApplicationOptions options) : base(options)
        {
        }

        [Route(RouteType = RouteType.Activity, Type = ActivityTypes.Message, Rank = RouteRank.Last)]
        protected Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        [Route(RouteType = RouteType.Event, EventName = "testEvent")]
        protected Task OnTestAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    class ActivityInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }
}
