// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Client
{
    public enum StreamResponseStatus
    {
        Pending,
        Complete,
        Error
    }

    public class StreamResponse<T>
    {
        public StreamResponseStatus Status { get; internal set; }
        public T Value { get; internal set; }
        public string Error { get; internal set; }
    }
}
