// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Specifies that a method should participate in continuing an existing conversation flow using optional route segment. 
    /// This is used by a Host to map incoming Proactive requests to the method.
    /// </summary>
    /// <remarks>Apply this attribute to methods that are intended to handle continuation of conversations,
    /// such as in conversational bots or workflow systems. The attribute identifies the delegate responsible for
    /// handling the continuation and optionally specifies a route segment to distinguish between different conversation
    /// paths. This attribute can be inherited by derived classes.</remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ContinueConversationAttribute : Attribute
    {
        public const string DefaultKey = "";

        /// <summary>
        /// Initializes a new instance of the ContinueConversationAttribute class with the specified conversation key
        /// and optional token handler configuration.
        /// </summary>
        /// <param name="key">The conversation key used to identify the conversation context. If null, an ArgumentNullException is thrown.</param>
        /// <param name="autoSignInHandlers">An optional comma, semicolon, or space delimted list of tokens to get.</param>
        /// <exception cref="ArgumentNullException">Thrown if the key parameter is null.</exception>
        public ContinueConversationAttribute(string key = DefaultKey, string autoSignInHandlers = null)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            TokenHandlers = autoSignInHandlers;
        }

        /// <summary>
        /// Gets the unique identifier associated with the current instance.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets or sets the token handler configuration used for processing authentication tokens.
        /// </summary>
        public string TokenHandlers { get; set; }
    }
}
