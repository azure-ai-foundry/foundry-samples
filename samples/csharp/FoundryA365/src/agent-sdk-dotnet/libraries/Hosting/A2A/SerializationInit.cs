// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using static Microsoft.Agents.Hosting.A2A.A2AJsonUtilities;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A
{
    [SerializationInit]
    internal class SerializationInit
    {
        public static void Init()
        {
            ProtocolJsonSerializer.AddTypeInfoResolver(JsonContext.Default);
        }
    }
}
