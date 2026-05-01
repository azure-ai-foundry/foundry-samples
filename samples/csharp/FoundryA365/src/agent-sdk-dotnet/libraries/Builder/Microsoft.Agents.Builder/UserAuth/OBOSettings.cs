// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Builder.UserAuth
{
    public class OBOSettings
    {
        public string OBOConnectionName { get; set; }
        public IList<string> OBOScopes { get; set; }
    }
}
