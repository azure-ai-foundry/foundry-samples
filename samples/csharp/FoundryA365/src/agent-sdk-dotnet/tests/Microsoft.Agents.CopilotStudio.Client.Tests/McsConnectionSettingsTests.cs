// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Agents.CopilotStudio.Client.Tests
{
    public class McsConnectionSettingsTests
    {
        [Fact]
        public void EmptySettings()
        {
            ConnectionSettings settings = new ConnectionSettings(null);
            Assert.NotNull(settings);
        }

        [Fact]
        public void InitWithValues()
        {
            var settings = new ConnectionSettings(null)
            {
                EnvironmentId = "envId",
                SchemaName = "botId",
                Cloud = Discovery.PowerPlatformCloud.Prod,
                CustomPowerPlatformCloud = "custom"
            };
            Assert.Equal("envId", settings.EnvironmentId);
            Assert.Equal(PowerPlatformCloud.Prod, settings.Cloud);
            Assert.Equal("custom", settings.CustomPowerPlatformCloud);
        }


        [Fact]
        public void InitFromConfig()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                                { "ConnectionTest:EnvironmentId", "envId" },
                                { "ConnectionTest:Cloud", PowerPlatformCloud.Prod.ToString() },
                                { "ConnectionTest:CustomPowerPlatformCloud", "foo.com" },
                                { "ConnectionTest:SchemaName", "agentSchema" },
                                { "ConnectionTest:CopilotAgentType", AgentType.Prebuilt.ToString() }
                })
                .Build();
            ConnectionSettings settings = new ConnectionSettings(configuration.GetSection("ConnectionTest"));
            Assert.Equal("envId", settings.EnvironmentId);
            Assert.Equal("agentSchema", settings.SchemaName);
            Assert.Equal("foo.com", settings.CustomPowerPlatformCloud);
            Assert.Equal(PowerPlatformCloud.Prod, settings.Cloud);
            Assert.Equal(AgentType.Prebuilt, settings.CopilotAgentType);
        }
    }
}