// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    public class ObjectTypeConverter : JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return SetGenericProperty(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value is string s)
            {
                if (!ProtocolJsonSerializer.UnpackObjectStrings)
                {
                    writer.WriteStringValue(s);
                }
                else
                {
                    // Generic property value as a JSON string
                    try
                    {
                        using (var document = JsonDocument.Parse(s))
                        {
                            var root = document.RootElement.Clone();
                            if (root.ValueKind == JsonValueKind.Object)
                            {
                                root.WriteTo(writer);
                            }
                            else
                            {
                                writer.WriteStringValue(s);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        writer.WriteStringValue(s);
                    }
                }
            }
            else
            {
                JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), options);
            }
        }

        /// <summary>
        /// This is to handle 'object' type properties that must conform to the original BF SDK handling.
        /// A simple type (string, int, etc...) is set as the value.  Complex objects are of type 'JsonElement'.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="options"></param>
        protected static object SetGenericProperty(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int i))
            {
                return i;
            }

            if (reader.TokenType == JsonTokenType.True)
            {
                return true;
            }
            
            if (reader.TokenType == JsonTokenType.False)
            {
                return false;
            }

            // Example: If it's a string, wrap it in a custom type
            if (reader.TokenType == JsonTokenType.String)
            {
                if (!ProtocolJsonSerializer.UnpackObjectStrings)
                {
                    return reader.GetString();
                }
                else
                {
                    try
                    {
                        var json = reader.GetString();

                        // Check if the underlying JSON is a reference type
                        using (var document = JsonDocument.Parse(json))
                        {
                            if (document.RootElement.ValueKind == JsonValueKind.Object || document.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                return document.RootElement.Clone();
                            }
                            else
                            {
                                return json;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // JSON is a value type
                        return reader.GetString();
                    }
                }
            }

            return JsonSerializer.Deserialize<JsonElement>(ref reader, options);
        }

    }
}
