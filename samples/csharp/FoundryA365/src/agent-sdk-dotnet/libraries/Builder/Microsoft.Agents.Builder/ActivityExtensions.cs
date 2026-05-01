// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder
{
    public static class ActivityExtensions
    {
        public static bool IsAgenticRequest(this IActivity activity)
        {
            return string.Equals(activity?.Recipient?.Role, RoleTypes.AgenticUser, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity?.Recipient?.Role, RoleTypes.AgenticIdentity, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Retrieves the tenant ID associated with the agentic recipient of the activity, if available; otherwise, returns the tenant ID from the conversation.
        /// </summary>
        /// <param name="activity">The activity from which to extract the tenant ID.</param>
        /// <returns>
        /// The tenant ID of the agentic recipient if present; otherwise, the tenant ID from the conversation. Returns <c>null</c> if neither is available.
        /// </returns>
        public static string GetAgenticTenantId(this IActivity activity)
        {
            return activity?.Recipient?.TenantId ?? activity?.Conversation?.TenantId;
        }

        public static string GetAgenticInstanceId(this IActivity activity)
        {
            if (!activity.IsAgenticRequest()) return null;
            return activity?.Recipient?.AgenticAppId;
        }

        public static string GetAgenticUser(this IActivity activity)
        {
            if (!activity.IsAgenticRequest()) return null;
            return activity?.Recipient?.AgenticUserId;
        }

        public static bool IsConnectorUser(this IActivity activity)
        {
            return string.Equals(activity?.Recipient?.Role, RoleTypes.ConnectorUser, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
