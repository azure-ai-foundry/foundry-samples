// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage;

namespace Microsoft.Agents.State.Tests
{
    public class TestState : IStoreItem
    {
        public string ETag { get; set; }

        public string Value { get; set; }
    }
}
