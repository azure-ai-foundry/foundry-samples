using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.JsonRpc;

/// <summary>
/// Represents a JSON-RPC ID that can be either a string or a number, preserving the original type.
/// </summary>
[JsonConverter(typeof(Converter))]
public readonly struct JsonRpcId : IEquatable<JsonRpcId>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Microsoft.Agents.Hosting.A2A.JsonRpc.JsonRpcId"/> struct with a string value.
    /// </summary>
    /// <param name="value">The string value.</param>
    public JsonRpcId(string? value) => Value = value;

    /// <summary>
    /// Initializes a new instance of the <see cref="Microsoft.Agents.Hosting.A2A.JsonRpc.JsonRpcId"/> struct with a numeric value.
    /// </summary>
    /// <param name="value">The numeric value.</param>
    public JsonRpcId(long value) => Value = value;

    /// <summary>
    /// Initializes a new instance of the <see cref="Microsoft.Agents.Hosting.A2A.JsonRpc.JsonRpcId"/> struct with a numeric value.
    /// </summary>
    /// <param name="value">The numeric value.</param>
    public JsonRpcId(int value) => Value = (long)value;

    /// <summary>
    /// Gets a value indicating whether this ID has a value.
    /// </summary>
    public bool HasValue => Value is not null;

    /// <summary>
    /// Gets a value indicating whether this ID is a string.
    /// </summary>
    public bool IsString => Value is string;

    /// <summary>
    /// Gets a value indicating whether this ID is a number.
    /// </summary>
    public bool IsNumber => Value is long;

    /// <summary>
    /// Gets the string value of this ID if it's a string.
    /// </summary>
    /// <returns>The string value, or null if not a string.</returns>
    public string? AsString() => Value as string;

    /// <summary>
    /// Gets the numeric value of this ID if it's a number.
    /// </summary>
    /// <returns>The numeric value, or null if not a number.</returns>
    public long? AsNumber() => Value as long?;

    /// <summary>
    /// Gets the raw value as an object.
    /// </summary>
    /// <returns>The raw value as an object.</returns>
    public object? Value { get; }

    /// <summary>
    /// Returns a string representation of this ID.
    /// </summary>
    /// <returns>A string representation of the ID.</returns>
    public override string? ToString() => Value?.ToString();

    /// <summary>
    /// Determines whether the specified object is equal to the current ID.
    /// </summary>
    /// <param name="obj">The object to compare with the current ID.</param>
    /// <returns>true if the specified object is equal to the current ID; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is JsonRpcId other && Equals(other);

    /// <summary>
    /// Determines whether the specified ID is equal to the current ID.
    /// </summary>
    /// <param name="other">The ID to compare with the current ID.</param>
    /// <returns>true if the specified ID is equal to the current ID; otherwise, false.</returns>
    public bool Equals(JsonRpcId other) => Equals(Value, other.Value);

    /// <summary>
    /// Returns the hash code for this ID.
    /// </summary>
    /// <returns>A hash code for the current ID.</returns>
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    /// <summary>
    /// Determines whether two IDs are equal.
    /// </summary>
    /// <param name="left">The first ID to compare.</param>
    /// <param name="right">The second ID to compare.</param>
    /// <returns>true if the IDs are equal; otherwise, false.</returns>
    public static bool operator ==(JsonRpcId left, JsonRpcId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two IDs are not equal.
    /// </summary>
    /// <param name="left">The first ID to compare.</param>
    /// <param name="right">The second ID to compare.</param>
    /// <returns>true if the IDs are not equal; otherwise, false.</returns>
    public static bool operator !=(JsonRpcId left, JsonRpcId right) => !left.Equals(right);

    /// <summary>
    /// Implicitly converts a string to a JsonRpcId.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>A JsonRpcId with the string value.</returns>
    public static implicit operator JsonRpcId(string? value) => new(value);

    /// <summary>
    /// Implicitly converts a long to a JsonRpcId.
    /// </summary>
    /// <param name="value">The numeric value.</param>
    /// <returns>A JsonRpcId with the numeric value.</returns>
    public static implicit operator JsonRpcId(long value) => new(value);

    /// <summary>
    /// Implicitly converts an int to a JsonRpcId.
    /// </summary>
    /// <param name="value">The numeric value.</param>
    /// <returns>A JsonRpcId with the numeric value.</returns>
    public static implicit operator JsonRpcId(int value) => new(value);

    /// <summary>
    /// JSON converter for JsonRpcId that preserves the original type during serialization/deserialization.
    /// </summary>
    internal sealed class Converter : JsonConverter<JsonRpcId>
    {
        public override JsonRpcId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return new JsonRpcId(reader.GetString());
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out var longValue))
                    {
                        return new JsonRpcId(longValue);
                    }
                    throw new JsonException("Invalid numeric value for JSON-RPC ID.");
                case JsonTokenType.Null:
                    return new JsonRpcId(null);
                default:
                    throw new JsonException("Invalid JSON-RPC ID format. Must be string, number, or null.");
            }
        }

        public override void Write(Utf8JsonWriter writer, JsonRpcId value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
            }
            else if (value.IsString)
            {
                writer.WriteStringValue(value.AsString());
            }
            else if (value.IsNumber)
            {
                writer.WriteNumberValue(value.AsNumber()!.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}