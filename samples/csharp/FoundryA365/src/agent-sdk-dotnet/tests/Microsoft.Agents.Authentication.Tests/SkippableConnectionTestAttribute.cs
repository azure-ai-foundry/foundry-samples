// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Xunit;

namespace Microsoft.Agents.Auth.Tests
{
    public class SkippableConnectionTestAttribute : FactAttribute
    {
        private static bool IsConnectionInfoAvailable() => Environment.GetEnvironmentVariable("XUNITAUTHTESTENABLED") != null;

        public SkippableConnectionTestAttribute()
        {
            if (!IsConnectionInfoAvailable())
            {
                Skip = "Ignored test as connection info is not present";
            }
        }

        public SkippableConnectionTestAttribute(bool skip, string skipMessage)
        {
            Skip = skipMessage;
        }
    }
}
