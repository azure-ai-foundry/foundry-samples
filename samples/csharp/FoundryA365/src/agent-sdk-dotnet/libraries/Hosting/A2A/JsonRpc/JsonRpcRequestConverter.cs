using Microsoft.Agents.Hosting.A2A.Protocol;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.JsonRpc;

/// <summary>
/// Custom JsonConverter for JsonRpcRequest that validates fields during deserialization.
/// </summary>
internal sealed class JsonRpcRequestConverter : JsonConverter<JsonRpcRequest>
{
    /// <summary>
    /// The supported JSON-RPC version.
    /// </summary>
    private const string JsonRpcSupportedVersion = "2.0";

    public override JsonRpcRequest? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            // Create JsonElement from Utf8JsonReader
            using var jsonDoc = JsonDocument.ParseValue(ref reader);
            var rootElement = jsonDoc.RootElement;

            // Validate the JSON-RPC request structure
            var idField = ReadAndValidateIdField(rootElement);
            var requestId = idField.ToString();
            return new JsonRpcRequest
            {
                Id = idField,
                JsonRpc = ReadAndValidateJsonRpcField(rootElement, requestId),
                Method = ReadAndValidateMethodField(rootElement, requestId),
                Params = ReadAndValidateParamsField(rootElement, requestId)
            };
        }
        catch (JsonException ex)
        {
            throw new A2AException("Invalid JSON-RPC request payload.", ex, A2AErrors.ParseError);
        }
    }

    public override void Write(Utf8JsonWriter writer, JsonRpcRequest value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "Cannot serialize a null JsonRpcRequest.");
        }

        writer.WriteStartObject();
        writer.WriteString("jsonrpc", value.JsonRpc);

        writer.WritePropertyName("id");
        if (!value.Id.HasValue)
        {
            writer.WriteNullValue();
        }
        else if (value.Id.IsString)
        {
            writer.WriteStringValue(value.Id.AsString());
        }
        else if (value.Id.IsNumber)
        {
            writer.WriteNumberValue(value.Id.AsNumber()!.Value);
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WriteString("method", value.Method);

        if (value.Params.HasValue)
        {
            writer.WritePropertyName("params");
            value.Params.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Reads and validates the 'id' field of a JSON-RPC request.
    /// </summary>
    /// <param name="rootElement">The root JSON element containing the request.</param>
    /// <returns>The extracted request ID as a JsonRpcId.</returns>
    private static JsonRpcId ReadAndValidateIdField(JsonElement rootElement)
    {
        if (rootElement.TryGetProperty("id", out var idElement))
        {
            if ((idElement.ValueKind != JsonValueKind.String &&
                idElement.ValueKind != JsonValueKind.Number &&
                idElement.ValueKind != JsonValueKind.Null)
                || (idElement.ValueKind is JsonValueKind.Number && !idElement.TryGetInt64(out var _)))
            {
                throw new A2AException("Invalid JSON-RPC request: 'id' field must be a string, non-fractional number, or null.", A2AErrors.InvalidRequest);
            }

            return idElement.ValueKind switch
            {
                JsonValueKind.Null => new JsonRpcId((string?)null),
                JsonValueKind.String => new JsonRpcId(idElement.GetString()),
                JsonValueKind.Number => new JsonRpcId(idElement.GetInt64()),
                _ => new JsonRpcId((string?)null)
            };
        }

        return new JsonRpcId((string?)null);
    }

    /// <summary>
    /// Reads and validates the 'jsonrpc' field of a JSON-RPC request.
    /// </summary>
    /// <param name="rootElement">The root JSON element containing the request.</param>
    /// <param name="requestId">The request ID for error context.</param>
    /// <returns>The JSON-RPC version as a string.</returns>
    private static string ReadAndValidateJsonRpcField(JsonElement rootElement, string? requestId)
    {
        if (rootElement.TryGetProperty("jsonrpc", out var jsonRpcElement))
        {
            var jsonRpc = jsonRpcElement.GetString();

            if (jsonRpc != JsonRpcSupportedVersion)
            {
                throw new A2AException("Invalid JSON-RPC request: 'jsonrpc' field must be '2.0'.", A2AErrors.InvalidRequest)
                    .WithRequestId(requestId);
            }

            return jsonRpc;
        }

        throw new A2AException("Invalid JSON-RPC request: missing 'jsonrpc' field.", A2AErrors.InvalidRequest)
            .WithRequestId(requestId);
    }

    /// <summary>
    /// Reads and validates the 'method' field of a JSON-RPC request.
    /// </summary>
    /// <param name="rootElement">The root JSON element containing the request.</param>
    /// <param name="requestId">The request ID for error context.</param>
    /// <returns>The method name as a string.</returns>
    private static string ReadAndValidateMethodField(JsonElement rootElement, string? requestId)
    {
        if (rootElement.TryGetProperty("method", out var methodElement))
        {
            var method = methodElement.GetString();
            if (string.IsNullOrEmpty(method))
            {
                throw new A2AException("Invalid JSON-RPC request: missing 'method' field.", A2AErrors.InvalidRequest)
                    .WithRequestId(requestId);
            }

            if (!A2AMethods.IsValidMethod(method!))
            {
                throw new A2AException("Invalid JSON-RPC request: 'method' field is not a valid A2A method.", A2AErrors.MethodNotFound)
                    .WithRequestId(requestId);
            }

            return method!;
        }

        throw new A2AException("Invalid JSON-RPC request: missing 'method' field.", A2AErrors.InvalidRequest)
            .WithRequestId(requestId);
    }

    /// <summary>
    /// Reads and validates the 'params' field of a JSON-RPC request.
    /// </summary>
    /// <param name="rootElement">The root JSON element containing the request.</param>
    /// <param name="requestId">The request ID for error context.</param>
    /// <returns>The 'params' element if it exists and is valid.</returns>
    private static JsonElement? ReadAndValidateParamsField(JsonElement rootElement, string? requestId)
    {
        if (rootElement.TryGetProperty("params", out var paramsElement))
        {
            if (paramsElement.ValueKind != JsonValueKind.Object &&
                paramsElement.ValueKind != JsonValueKind.Undefined &&
                paramsElement.ValueKind != JsonValueKind.Null)
            {
                throw new A2AException("Invalid JSON-RPC request: 'params' field must be an object or null.", A2AErrors.InvalidParams)
                    .WithRequestId(requestId);
            }
        }

        return paramsElement.ValueKind == JsonValueKind.Null || paramsElement.ValueKind == JsonValueKind.Undefined
            ? null
            : paramsElement.Clone();
    }
}