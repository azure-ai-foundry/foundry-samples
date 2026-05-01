// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Core.Validation
{
    [Flags]
    public enum ValidationContext
    {
        Channel = 0x01,
        Agent = 0x02,
        Sender = 0x04,
        Receiver = 0x08
    }
}
