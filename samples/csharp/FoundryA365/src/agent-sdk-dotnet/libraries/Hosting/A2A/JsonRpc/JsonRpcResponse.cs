using Microsoft.Agents.Hosting.A2A.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Microsoft.Agents.Hosting.A2A.JsonRpc;

/// <summary>
/// Represents a JSON-RPC 2.0 Response object.
/// </summary>
internal sealed class JsonRpcResponse
{
    /// <summary>
    /// Gets or sets the version of the JSON-RPC protocol. MUST be exactly "2.0".
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// Gets or sets the identifier established by the Client.
    /// </summary>
    /// <remarks>
    /// MUST contain a String, Number. Numbers SHOULD NOT contain fractional parts.
    /// </remarks>
    [JsonPropertyName("id")]
    public JsonRpcId Id { get; set; }

    /// <summary>
    /// Gets or sets the result object on success.
    /// </summary>
    [JsonPropertyName("result")]
    public JsonNode? Result { get; set; }

    /// <summary>
    /// Gets or sets the error object when an error occurs.
    /// </summary>
    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }

    /// <summary>
    /// Creates a JSON-RPC response with a result.
    /// </summary>
    /// <typeparam name="T">The type of the result</typeparam>
    /// <param name="requestId">The request ID.</param>
    /// <param name="result">The result to include.</param>
    /// <param name="resultTypeInfo">Optional type information for serialization.</param>
    /// <returns>A JSON-RPC response object.</returns>
    public static JsonRpcResponse CreateJsonRpcResponse<T>(JsonRpcId requestId, T result, JsonTypeInfo? resultTypeInfo = null)
    {
        resultTypeInfo ??= (JsonTypeInfo<T>)A2AJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));

        return new JsonRpcResponse()
        {
            Id = requestId,
            Result = result is not null ? JsonSerializer.SerializeToNode(result, resultTypeInfo) : null
        };
    }

    /// <summary>
    /// Creates a JSON-RPC error response for a given exception.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="exception">The exception containing error details.</param>
    /// <returns>A JSON-RPC error response.</returns>
    public static JsonRpcResponse CreateJsonRpcErrorResponse(JsonRpcId requestId, A2AException exception)
    {
        return new JsonRpcResponse()
        {
            Id = requestId,
            Error = new JsonRpcError
            {
                Code = (int)exception.ErrorCode,
                Message = exception.Message,
            }
        };
    }

    /// <summary>
    /// Creates a JSON-RPC error response for invalid parameters.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A JSON-RPC error response.</returns>
    public static JsonRpcResponse InvalidParamsResponse(JsonRpcId requestId, string? message = null) => new()
    {
        Id = requestId,
        Error = new JsonRpcError()
        {
            Code = (int)A2AErrors.InvalidParams,
            Message = message ?? "Invalid parameters",
        },
    };

    /// <summary>
    /// Creates a JSON-RPC error response for invalid request.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="message">Optional error message.</param>
    /// <returns>A JSON-RPC error response.</returns>
    public static JsonRpcResponse InvalidRequestResponse(JsonRpcId requestId, string? message = null) => new()
    {
        Id = requestId,
        Error = new JsonRpcError
        {
            Code = A2AErrors.InvalidRequest,
            Message = message ?? "Request payload validation error",
        },
    };

    /// <summary>
    /// Creates a JSON-RPC error response for task not found.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A JSON-RPC error response.</returns>
    public static JsonRpcResponse TaskNotFoundResponse(JsonRpcId requestId, string? message = null) => new()
    {
        Id = requestId,
        Error = new JsonRpcError
        {
            Code = A2AErrors.TaskNotFound,
            Message = message ?? "Task not found",
        },
    };

    /// <summary>
    /// Creates a JSON-RPC error response for task not cancelable.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A JSON-RPC error response.</returns>
    public static JsonRpcResponse TaskNotCancelableResponse(JsonRpcId requestId, string? message = null) => new()
    {
        Id = requestId,
        Error = new JsonRpcError
        {
            Code = A2AErrors.TaskNotCancelable,
            Message = message ?? "Task cannot be canceled",
        },
    };

    /// <summary>
    /// Creates a JSON-RPC error response for method not found.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A JSON-RPC error response.</returns>
    public static JsonRpcResponse MethodNotFoundResponse(JsonRpcId requestId, string? message = null) => new()
    {
        Id = requestId,
        Error = new JsonRpcError
        {
            Code = A2AErrors.MethodNotFound,
            Message = message ?? "Method not found",
        },
    };

    /// <summary>
    /// Creates a JSON-RPC error response for push notification not supported.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A JSON-RPC error response.</returns>
    public static JsonRpcResponse PushNotificationNotSupportedResponse(JsonRpcId requestId, string? message = null) => new()
    {
        Id = requestId,
        Error = new JsonRpcError
        {
            Code = A2AErrors.PushNotificationNotSupported,
            Message = message ?? "Push notification not supported",
        },
    };

    /// <summary>
    /// Creates a JSON-RPC error response for internal error.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A JSON-RPC error response.</returns>
    public static JsonRpcResponse InternalErrorResponse(JsonRpcId requestId, string? message = null) => new()
    {
        Id = requestId,
        Error = new JsonRpcError
        {
            Code = A2AErrors.InternalError,
            Message = message ?? "Internal error",
        },
    };

    /// <summary>
    /// Creates a JSON-RPC error response for parse error.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A JSON-RPC error response.</returns>
    public static JsonRpcResponse ParseErrorResponse(JsonRpcId requestId, string? message = null) => new()
    {
        Id = requestId,
        Error = new JsonRpcError
        {
            Code = A2AErrors.ParseError,
            Message = message ?? "Invalid JSON payload"
        },
    };

    /// <summary>
    /// Creates a JSON-RPC error response for unsupported operation.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A JSON-RPC error response.</returns>
    public static JsonRpcResponse UnsupportedOperationResponse(JsonRpcId requestId, string? message = null) => new()
    {
        Id = requestId,
        Error = new JsonRpcError
        {
            Code = A2AErrors.UnsupportedOperation,
            Message = message ?? "Unsupported operation",
        },
    };

    /// <summary>
    /// Creates a JSON-RPC error response for content type not supported.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A JSON-RPC error response.</returns>
    public static JsonRpcResponse ContentTypeNotSupportedResponse(JsonRpcId requestId, string? message = null) => new()
    {
        Id = requestId,
        Error = new JsonRpcError
        {
            Code = A2AErrors.ContentTypeNotSupported,
            Message = message ?? "Content type not supported",
        },
    };
}