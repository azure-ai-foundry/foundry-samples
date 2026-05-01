// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Constants used to populate the <see cref="Microsoft.Agents.Core.Models.Activity.CallerId"/> property.
    /// </summary>
    public static class CallerIdConstants
    {
        /// <summary>
        ///  The caller ID for any Azure Channel.
        /// </summary>
        public const string PublicAzureChannel = "urn:botframework:azure";

        /// <summary>
        ///  The caller ID for any Azure US Government cloud channel.
        /// </summary>
        public const string USGovChannel = "urn:botframework:azureusgov";

        /// <summary>
        /// The caller ID prefix when an Agent initiates a request to another Agent.
        /// </summary>
        /// <remarks>
        /// This prefix will be followed by the Azure Active Directory App ID of the Agent that initiated the call.
        /// </remarks>
        public const string AgentPrefix = "urn:botframework:aadappid:";
    }
}
