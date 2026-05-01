// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Storage
{
    public class EtagException : Exception
    {
        public EtagException()
        {
        }

        public EtagException(string message) : base(message)
        {
        }

        public EtagException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
