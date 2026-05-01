// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.UserAuth
{
    /// <summary>
    /// The sign-in status
    /// </summary>
    public enum SignInStatus
    {
        /// <summary>
        /// Sign-in not complete and requires user interaction
        /// </summary>
        Pending,

        /// <summary>
        /// Sign-in complete
        /// </summary>
        Complete,

        /// <summary>
        /// Error occurred during sign-in
        /// </summary>
        Error,

        Duplicate
    }
}
