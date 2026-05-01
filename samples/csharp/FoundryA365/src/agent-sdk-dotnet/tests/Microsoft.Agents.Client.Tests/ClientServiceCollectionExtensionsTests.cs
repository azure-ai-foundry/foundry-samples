// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class ClientServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddChannelHost_ShouldSetServices()
        {
            var builder = new Mock<IHostApplicationBuilder>();
            builder.SetupGet(e => e.Services).Returns(new ServiceCollection());
            ClientServiceCollectionExtensions.AddAgentHost(builder.Object);

            var services = builder.Object.Services
                .Select(e => e.ImplementationType ?? e.ServiceType)
                .ToList();

            Assert.True(services.Where(s => s == typeof(AdapterChannelResponseHandler)).Any());
            Assert.True(services.Where(s => s == typeof(IAgentHost)).Any());
        }
    }
}