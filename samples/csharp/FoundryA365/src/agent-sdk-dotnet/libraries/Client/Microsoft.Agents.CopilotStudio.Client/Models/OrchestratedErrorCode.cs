// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Known error codes returned from externally orchestrated conversation turns.
    /// </summary>
    public enum OrchestratedErrorCode
    {
        /// <summary>
        /// The error code is not recognized or was not provided.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The requested operation is not valid in the current context.
        /// </summary>
        InvalidOperation,

        /// <summary>
        /// A required field was missing from the request.
        /// </summary>
        MissingRequiredField,

        /// <summary>
        /// The requested operation is not valid for the current conversation state.
        /// </summary>
        InvalidOperationForState,

        /// <summary>
        /// Authentication failed (HTTP 401/403).
        /// </summary>
        Unauthorized,

        /// <summary>
        /// The request was throttled (HTTP 429).
        /// </summary>
        Throttled,

        /// <summary>
        /// The bot content could not be found.
        /// </summary>
        BotContentNotFound,

        /// <summary>
        /// The specified topic was not found.
        /// </summary>
        TopicNotFound,

        /// <summary>
        /// An internal server error occurred.
        /// </summary>
        InternalServerError,

        /// <summary>
        /// The specified conversation was not found.
        /// </summary>
        ConversationNotFound
    }

    /// <summary>
    /// Extension methods for parsing <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedErrorCode"/> from error code strings.
    /// </summary>
    public static class OrchestratedErrorCodeExtensions
    {
        /// <summary>
        /// Parses a string error code into an <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedErrorCode"/> enum value.
        /// Returns <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedErrorCode.Unknown"/> if the code is null, empty, or not recognized.
        /// </summary>
        /// <param name="code">The error code string from the error payload.</param>
        /// <returns>The corresponding <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedErrorCode"/> value.</returns>
        public static OrchestratedErrorCode ToOrchestratedErrorCode(this string? code)
        {
            if (!string.IsNullOrEmpty(code)
                && Enum.TryParse<OrchestratedErrorCode>(code, ignoreCase: true, out var result))
            {
                return result;
            }

            return OrchestratedErrorCode.Unknown;
        }
    }
}
