// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;

namespace Microsoft.Agents.Storage.Tests
{
    public class TestTraceListener : TraceListener
    {
        private readonly StringBuilder _messages = new StringBuilder();

        public override void Write(string message)
        {
            _messages.Append(message);
        }

        public override void WriteLine(string message)
        {
            _messages.AppendLine(message);
        }

        public string GetMessages()
        {
            return _messages.ToString();
        }

        public void Clear()
        {
            _messages.Clear();
        }
    }
}
