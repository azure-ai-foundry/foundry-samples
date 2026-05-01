// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Hosting.A2A.Protocol;

internal static class A2AMethods
{
    public const string MessageStream = "message/stream";
    public const string MessageSend = "message/send";
    public const string TasksResubscribe = "tasks/resubscribe";
    public const string TasksGet = "tasks/get";
    public const string TasksCancel = "tasks/cancel";
    public const string TasksPushNotificationSet = "tasks/pushNotificationConfig/set";
    public const string TasksPushNotificationGet = "tasks/pushNotificationConfig/get";
    public const string TasksPushNotificationList = "tasks/pushNotificationConfig/list";
    public const string TasksPushNotificationDelete = "tasks/pushNotificationConfig/delete";
    public const string AgentAuthenticationCard = "agent/getAuthenticatedExtendedCard";

    /// <summary>
    /// Determines if a method name is valid for A2A JSON-RPC.
    /// </summary>
    /// <param name="method">The method name to validate.</param>
    /// <returns>True if the method is valid, false otherwise.</returns>
    public static bool IsValidMethod(string method) => method is
        MessageSend or
        MessageStream or
        TasksGet or
        TasksCancel or
        TasksResubscribe or
        TasksPushNotificationSet or
        TasksPushNotificationGet or
        TasksPushNotificationList or
        TasksPushNotificationDelete;
}
