// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// A transport protocol for an <see cref="Microsoft.Agents.Hosting.A2A.Protocol.AgentInterface"/>.
/// </summary>
[JsonConverter(typeof(TransportProtocolConverter))]
public class TransportProtocol : IEquatable<TransportProtocol>
{
    private readonly string _protocol;

    private static readonly TransportProtocol _jsonrpc = new("JSONRPC");
    private static readonly TransportProtocol _grpc = new("GRPC");
    private static readonly TransportProtocol _httpjson = new("HTTP+JSON");

    public static TransportProtocol JsonRpc 
    {
        get { return _jsonrpc; } 
    }

    public static TransportProtocol GRpc
    {
        get { return _grpc; } 
    }

    public static TransportProtocol HttpJson
    {
        get { return _httpjson; }
    }

    [JsonConstructor]
    public TransportProtocol(string protocol)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(nameof(protocol), "Transport protocol cannot be null or whitespace");
        _protocol = protocol;
    }

    public static bool operator ==(TransportProtocol left, TransportProtocol right)
        => left.Equals(right);

    public static bool operator !=(TransportProtocol left, TransportProtocol right)
        => !(left == right);

    public bool Equals(TransportProtocol other)
    {
        if (other is null)
        {
            return false;
        }

        if (object.ReferenceEquals(_protocol, other._protocol))
        {
            // Strings are interned, so there is a good chance that two equal methods use the same reference
            // (unless they differ in case).
            return true;
        }

        return string.Compare(_protocol, other._protocol, StringComparison.OrdinalIgnoreCase) == 0;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as TransportProtocol);
    }

    public override int GetHashCode()
    {
        return _protocol.ToUpperInvariant().GetHashCode();
    }

    public override string ToString()
    {
        return _protocol.ToString();
    }

    internal sealed class TransportProtocolConverter : JsonConverter<TransportProtocol>
    {
        public override TransportProtocol Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Expected a string for transport.");
            }

            var protocol = reader.GetString();
            if (string.IsNullOrWhiteSpace(protocol))
            {
                throw new JsonException("transport string value cannot be null or whitespace.");
            }

            return new TransportProtocol(protocol!);
        }

        public override void Write(Utf8JsonWriter writer, TransportProtocol value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value._protocol);
        }
    }
}