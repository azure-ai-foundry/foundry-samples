using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.JsonRpc;

/// <summary>
/// Represents a JSON-RPC 2.0 Error object.
/// </summary>
public class JsonRpcError
{
    /// <summary>
    /// Gets or sets the number that indicates the error type that occurred.
    /// </summary>
    [JsonPropertyName("code")]
    [JsonRequired]
    public int Code { get; set; } = 0;

    /// <summary>
    /// Gets or sets the string providing a short description of the error.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonRequired]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primitive or structured value that contains additional information about the error.
    /// This may be omitted.
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }

    /// <summary>
    /// Deserializes a JsonRpcError from a JsonElement.
    /// </summary>
    /// <param name="jsonElement">The JSON element to deserialize.</param>
    /// <returns>A JsonRpcError instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when deserialization fails.</exception>
    public static JsonRpcError FromJson(JsonElement jsonElement) =>
        jsonElement.Deserialize(A2AJsonUtilities.JsonContext.Default.JsonRpcError) ??
        throw new InvalidOperationException("Failed to deserialize JsonRpcError.");

    /// <summary>
    /// Serializes a JsonRpcError to JSON.
    /// </summary>
    /// <returns>JSON string representation.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, A2AJsonUtilities.JsonContext.Default.JsonRpcError);
}