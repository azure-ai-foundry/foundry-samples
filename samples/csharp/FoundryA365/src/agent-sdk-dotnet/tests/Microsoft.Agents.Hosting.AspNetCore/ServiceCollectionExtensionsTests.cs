// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddCloudAdapter_ShouldSetServices()
        {
            var collection = new ServiceCollection();
            collection.AddCloudAdapter();

            var services = collection
                .Select(e => e.ImplementationType ?? e.ServiceType)
                .ToList();
            var expected = new List<Type>{
                typeof(HostedActivityService),
                typeof(HostedTaskService),
                typeof(BackgroundTaskQueue),
                typeof(ActivityTaskQueue),
                typeof(CloudAdapter), // Default Type passed to AddCloudAdapter.
                typeof(IAgentHttpAdapter),
                typeof(IChannelAdapter),
            };

            Assert.Equal(expected, services);
        }

        [Fact]
        public void AddBot_ShouldSetServices()
        {
            var builder = new Mock<IHostApplicationBuilder>();
            builder.SetupGet(e => e.Services).Returns(new ServiceCollection());
            ServiceCollectionExtensions.AddAgent<ActivityHandler>(builder.Object);

            var services = builder.Object.Services
                .Select(e => e.ImplementationType ?? e.ServiceType)
                .ToList();
            var expected = new List<Type>{
                typeof(ConfigurationConnections),
                typeof(RestChannelServiceClientFactory),
                // CloudAdapter services.
                typeof(HostedActivityService),
                typeof(HostedTaskService),
                typeof(BackgroundTaskQueue),
                typeof(ActivityTaskQueue),
                typeof(CloudAdapter),
                typeof(IAgentHttpAdapter),
                typeof(IChannelAdapter),
                typeof(ActivityHandler), // IAgent.
                typeof(ActivityHandler), // TAgent.
            };

            Assert.Equal(expected, services);
        }
    }
}