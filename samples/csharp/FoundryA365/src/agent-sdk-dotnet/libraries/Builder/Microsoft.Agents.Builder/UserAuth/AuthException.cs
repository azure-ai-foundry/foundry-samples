// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.UserAuth
{
    /// <summary>
    /// Cause of user authentication exception.
    /// </summary>
    public enum AuthExceptionReason
    {
        /// <summary>
        /// The authentication flow completed without a token.
        /// </summary>
        CompletionWithoutToken,

        /// <summary>
        /// The the flow timed out (C2 didn't respond in time).
        /// </summary>
        Timeout,

        InvalidSignIn,

        Exception,

        UserCancelled,

        /// <summary>
        /// Other error.
        /// </summary>
        Other
    }

    /// <summary>
    /// An exception thrown when user authentication error occurs.
    /// </summary>
    /// <remarks>
    /// Initializes the class
    /// </remarks>
    /// <param name="message">The exception message</param>
    /// <param name="reason">The cause of the exception</param>
    /// <param name="inner"></param>
    internal class AuthException(string message, AuthExceptionReason reason = AuthExceptionReason.Other, Exception inner = null) : Exception(message, inner)
    {
        /// <summary>
        /// The cause of the exception.
        /// </summary>
        public AuthExceptionReason Cause { get; } = reason;
    }
}
