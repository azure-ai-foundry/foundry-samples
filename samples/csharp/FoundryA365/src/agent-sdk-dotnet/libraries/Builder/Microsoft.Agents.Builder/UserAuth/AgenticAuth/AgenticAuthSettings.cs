// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Builder.UserAuth.AgenticAuth
{
    /// <summary>
    /// The settings for AgenticAuth Authorization.
    /// </summary>
    public class AgenticAuthSettings
    {
        public IList<string> Scopes { get; set; }
        public string AlternateBlueprintConnectionName { get; set; } = null; 
    }
}
