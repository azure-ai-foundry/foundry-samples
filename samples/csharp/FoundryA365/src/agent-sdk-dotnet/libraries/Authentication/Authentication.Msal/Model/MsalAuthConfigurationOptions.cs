// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using System;

namespace Microsoft.Agents.Authentication.Msal.Model
{
    /// <summary>
    /// Configuration Options for MSAL Authentication.
    /// </summary>
    internal class MsalAuthConfigurationOptions
    {
        /// <summary>
        /// Create MSAL Configuration Options from Configuration Section to support configuring MSAL Auth with Dependency Injection.
        /// </summary>
        /// <param name="configurationSection"></param>
        /// <returns></returns>
        public static MsalAuthConfigurationOptions CreateFromConfigurationOptions(IConfigurationSection configurationSection)
        {
            var config = new MsalAuthConfigurationOptions();
            if (configurationSection != null)
            {
                config.MSALEnabledLogPII = configurationSection.GetValue<bool>("MSALEnabledLogPII", false);
                config.MSALRequestTimeout = configurationSection.GetValue<TimeSpan>("MSALRequestTimeout", new TimeSpan(0, 0, 0, 30));
                config.MSALRetryCount = configurationSection.GetValue<int>("MSALRetryCount", 3);
            }
            return config; 
        }


        /// <summary>
        /// Updates the instance of Options with a previously created Options Object. 
        /// </summary>
        /// <param name="options">PreLoaded Options Array</param>
        public void UpdateOptions(MsalAuthConfigurationOptions options)
        {
            if (options != null)
            {
                MSALEnabledLogPII = options.MSALEnabledLogPII;
                MSALRequestTimeout = options.MSALRequestTimeout;
                MSALRetryCount = options.MSALRetryCount;
            }
        }

        #region MSAL Settings.
        private TimeSpan _msalTimeout = Utils.AppSettingsHelper.GetAppSettingTimeSpan("MSALRequestTimeoutOverride", Utils.AppSettingsHelper.TimeSpanFromKey.Seconds, new TimeSpan(0, 0, 0, 30));

        /// <summary>
        /// Amount of time to wait for MSAL/AAD to wait for a token response before timing out
        /// </summary>
        public TimeSpan MSALRequestTimeout
        {
            get => _msalTimeout;
            set => _msalTimeout = value;
        }

        private int _msalRetryCount = Utils.AppSettingsHelper.GetAppSetting("MSALRequestRetryCountOverride", 3);

        /// <summary>
        /// Number of retries to Get a token from MSAL.
        /// </summary>
        public int MSALRetryCount
        {
            get => _msalRetryCount;
            set => _msalRetryCount = value;
        }

        private bool _msalEnablePIIInLog = Utils.AppSettingsHelper.GetAppSetting("MSALLogPII", false);

        /// <summary>
        /// Enabled Logging of PII in MSAL Log. - defaults to false.
        /// </summary>
        public bool MSALEnabledLogPII
        {
            get => _msalEnablePIIInLog;
            set => _msalEnablePIIInLog = value;
        }
        #endregion
    }

}
