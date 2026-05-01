// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests.Model
{
    public class MsalAuthConfigurationOptionsTests
    {
        [Fact]
        public void CreateFromConfigurationOptions_ShouldReturnDefaultOptionsOnNullConfig()
        {
            var options = MsalAuthConfigurationOptions.CreateFromConfigurationOptions(null);

            Assert.NotNull(options);
            Assert.False(options.MSALEnabledLogPII);
            Assert.Equal(3, options.MSALRetryCount);
            Assert.Equal(new TimeSpan(0, 0, 0, 30), options.MSALRequestTimeout);
        }

        [Fact]
        public void CreateFromConfigurationOptions_ShouldReturnCustomOptionsFromConfig()
        {
            var configSettings = new Dictionary<string, string> {
                { "MsalOptions:MSALEnabledLogPII", "true" },
                { "MsalOptions:MSALRetryCount", "5" },
                { "MsalOptions:MSALRequestTimeout", "00:00:01:00" },
            };
            var SettingsSection = "MsalOptions";

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var options = MsalAuthConfigurationOptions.CreateFromConfigurationOptions(configuration.GetSection(SettingsSection));

            Assert.NotNull(options);
            Assert.True(options.MSALEnabledLogPII);
            Assert.Equal(5, options.MSALRetryCount);
            Assert.Equal(new TimeSpan(0, 0, 1, 00), options.MSALRequestTimeout);
        }

        [Fact]
        public void UpdateOptions_ShouldUpdateDeaultValues()
        {
            var options = MsalAuthConfigurationOptions.CreateFromConfigurationOptions(null);

            var updatedOptions = new MsalAuthConfigurationOptions()
            {
                MSALEnabledLogPII = true,
                MSALRetryCount = 5,
                MSALRequestTimeout = TimeSpan.FromSeconds(1),
            };

            options.UpdateOptions(updatedOptions);

            Assert.Equal(updatedOptions.MSALEnabledLogPII, options.MSALEnabledLogPII);
            Assert.Equal(updatedOptions.MSALRetryCount, options.MSALRetryCount);
            Assert.Equal(updatedOptions.MSALRequestTimeout, options.MSALRequestTimeout);
        }
    }
}
