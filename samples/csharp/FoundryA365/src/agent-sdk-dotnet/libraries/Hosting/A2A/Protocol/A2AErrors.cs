// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Hosting.A2A.Protocol;

internal static class A2AErrors
{
    /// <summary>
    /// Invalid JSON payload Server received JSON that was not well-formed.
    /// </summary>
    /// <remarks>
    /// Standard JSON-RPC error code for invalid JSON payloads.
    /// </remarks>
    public static int ParseError = -32700;

    /// <summary>
    /// The JSON payload was valid JSON, but not a valid JSON-RPC Request object.
    /// </summary>
    /// <remarks>
    /// Standard JSON-RPC error code for invalid JSON payloads.
    /// </remarks>
    public static int InvalidRequest = -32600;

    /// <summary>
    /// The requested A2A RPC method(e.g., "tasks/foo") does not exist or is not supported.
    /// </summary>
    /// <remarks>
    /// Standard JSON-RPC error code for invalid JSON payloads.
    /// </remarks>
    public static int MethodNotFound = -32601;

    /// <summary>
    /// Invalid method parameters The params provided for the method are invalid (e.g., wrong type, missing required field).
    /// </summary>
    /// <remarks>
    /// Standard JSON-RPC error code for invalid JSON payloads.
    /// </remarks>
    public static int InvalidParams = -32602;

    /// <summary>
    /// Internal server error An unexpected error occurred on the server during processing.
    /// </summary>
    /// <remarks>
    /// Standard JSON-RPC error code for invalid JSON payloads.
    /// </remarks>
    public static int InternalError = -32603;

    /// <summary>
    /// to -32099	    (Server-defined)    Reserved for implementation-defined server-errors.A2A-specific errors use this range.
    /// </summary>
    /// <remarks>
    /// Standard JSON-RPC error code for invalid JSON payloads.
    /// </remarks>
    public static int ServerError = -32000;

    /// <summary>
    /// Task not found  The specified task id does not correspond to an existing or active task.It might be invalid, expired, or already completed and purged.
    /// </summary>
    public static int TaskNotFound = -32001;

    /// <summary>
    /// Task cannot be canceled An attempt was made to cancel a task that is not in a cancelable state (e.g., it has already reached a terminal state like completed, failed, or canceled).
    /// </summary>
    public static int TaskNotCancelable = -32002;

    /// <summary>
    /// Push Notification is not supported  Client attempted to use push notification features(e.g., tasks/pushNotificationConfig/set) but the server agent does not support them(i.e., AgentCard.capabilities.pushNotifications is false).
    /// </summary>
    public static int PushNotificationNotSupported = -32003;

    /// <summary>
    /// This operation is not supported The requested operation or a specific aspect of it(perhaps implied by parameters) is not supported by this server agent implementation.Broader than just method not found.
    /// </summary>
    public static int UnsupportedOperation = -32004;

    /// <summary>
    /// Incompatible content types  A Media Type provided in the request's message.parts (or implied for an artifact) is not supported by the agent or the specific skill being invoked.
    /// </summary>
    public static int ContentTypeNotSupported = -32005;

    /// <summary>
    /// Invalid agent response type Agent generated an invalid response for the requested method.
    /// </summary>
    public static int InvalidAgentResponse = -32006;

    /// <summary>
    /// The agent does not have an Authenticated Extended Card configured.
    /// </summary>
    public static int AuthenticatedExtendedCardNotConfigured = -32007;
}
