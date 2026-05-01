// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;


namespace Microsoft.Agents.CopilotStudio.Client.Discovery
{
    /// <summary>
    /// This is the Power Platform Cloud you are attempting to connect to.
    /// </summary>
    public enum PowerPlatformCloud
    {
        /// <summary>
        /// Unknown Power Platform Cloud.
        /// </summary>
        Unknown = -1,
        /// <summary>
        /// Internal Use Only
        /// </summary>
        [EnumMember(Value = "Exp")]
        Exp = 0,

        /// <summary>
        /// Internal Use Only
        /// </summary>
        [EnumMember(Value = "Dev")]
        Dev = 1,

        /// <summary>
        /// Internal Use Only
        /// </summary>
        [EnumMember(Value = "Test")]
        Test = 2,

        /// <summary>
        /// Internal Use Only
        /// </summary>
        [EnumMember(Value = "Preprod")]
        Preprod = 3,

        /// <summary>
        /// Environments that have been created in the First Release Cloud.
        /// </summary>
        [EnumMember(Value = "FirstRelease")]
        FirstRelease = 4,

        /// <summary>
        /// Environments that have been created in the Production Cloud. This means all Geo's except for the First Release Geo and Sovereign Clouds.
        /// </summary>
        [EnumMember(Value = "Prod")]
        Prod = 5,

        /// <summary>
        /// United States GCC Cloud
        /// </summary>
        [EnumMember(Value = "Gov")]
        Gov = 6,

        /// <summary>
        /// United States Gov High Sovereign Cloud
        /// </summary>
        [EnumMember(Value = "High")]
        High = 7,

        /// <summary>
        /// United States DoD Sovereign Cloud
        /// </summary>
        [EnumMember(Value = "DoD")]
        DoD = 8,

        /// <summary>
        /// China Sovereign Cloud
        /// </summary>
        [EnumMember(Value = "Mooncake")]
        Mooncake = 9,

        /// <summary>
        /// Restricted Sovereign Cloud
        /// </summary>
        [EnumMember(Value = "Ex")]
        Ex = 10,

        /// <summary>
        /// Restricted Sovereign Cloud
        /// </summary>
        [EnumMember(Value = "Rx")]
        Rx = 11,

        /// <summary>
        /// Prv is short for Pull Request Validation.
        /// The clusters of this category in <see cref="Microsoft.Agents.CopilotStudio.Client.Discovery.PowerPlatformCloud.Test"/> is used for deploying validation instance during pull request.
        /// </summary>
        [EnumMember(Value = "Prv")]
        Prv = 12,

        /// <summary>
        /// Internal Use Only 
        /// </summary>
        [EnumMember(Value = "Local")]
        Local = 13,

        /// <summary>
        /// French Government Sovereign Cloud
        /// </summary>
        [EnumMember(Value = "GovFR")]
        GovFR = 14,

        /// <summary>
        /// Used to Specify a Custom Cloud PowerPlatform API Base Address
        /// </summary>
        [EnumMember(Value = "Other")]
        Other = 100,
    }
}
