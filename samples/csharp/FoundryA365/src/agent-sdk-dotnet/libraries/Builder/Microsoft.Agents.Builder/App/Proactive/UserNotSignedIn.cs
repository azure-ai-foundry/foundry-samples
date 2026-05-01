// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Exception thrown when a user tokens are required in <c>Proactive.ContinueConversation</c> but the user has not previously signed in.
    /// </summary>
    public class UserNotSignedIn : InvalidOperationException
    {
        public UserNotSignedIn() : base("User is not signed in") { }

        public UserNotSignedIn(string message) : base(message) { }

        public UserNotSignedIn(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
