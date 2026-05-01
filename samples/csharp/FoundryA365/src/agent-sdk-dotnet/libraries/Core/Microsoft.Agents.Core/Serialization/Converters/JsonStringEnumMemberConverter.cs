// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    /// <summary>
    /// JSON converter that handles enum serialization using EnumMember attributes.
    /// Reads and writes enum values based on their EnumMember Value property.
    /// </summary>
    public class JsonStringEnumMemberConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        private static readonly ConcurrentDictionary<Type, Dictionary<string, T>> _stringToEnumCache = new();
        private static readonly ConcurrentDictionary<Type, Dictionary<T, string>> _enumToStringCache = new();

        /// <summary>
        /// Reads JSON and converts it to the enum value using EnumMember attributes.
        /// </summary>
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Unable to convert \"{reader.GetString()}\" to enum \"{typeof(T)}\".");
            }

            var stringValue = reader.GetString();
            if (string.IsNullOrEmpty(stringValue))
            {
                throw new JsonException($"Unable to convert empty or null string to enum \"{typeof(T)}\".");
            }

            var stringToEnumMap = GetStringToEnumMap();
            
            if (stringToEnumMap.TryGetValue(stringValue, out var enumValue))
            {
                return enumValue;
            }

            // Fallback to default enum parsing
            if (Enum.TryParse<T>(stringValue, true, out enumValue))
            {
                return enumValue;
            }

            throw new JsonException($"Unable to convert \"{stringValue}\" to enum \"{typeof(T)}\".");
        }

        /// <summary>
        /// Writes the enum value to JSON using EnumMember attributes.
        /// </summary>
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var enumToStringMap = GetEnumToStringMap();
            
            if (enumToStringMap.TryGetValue(value, out var stringValue))
            {
                writer.WriteStringValue(stringValue);
            }
            else
            {
                // Fallback to enum name
                writer.WriteStringValue(value.ToString());
            }
        }

        /// <summary>
        /// Gets the mapping from string values to enum values.
        /// </summary>
        private static Dictionary<string, T> GetStringToEnumMap()
        {
            return _stringToEnumCache.GetOrAdd(typeof(T), _ =>
            {
                var map = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
#pragma warning disable CA2263 // Prefer generic overload when type is known
                var enumValues = Enum.GetValues(typeof(T));
#pragma warning restore CA2263 // Prefer generic overload when type is known

                foreach (var enumValue in enumValues)
                {
                    var field = typeof(T).GetField(enumValue.ToString());
                    var enumMemberAttribute = field?.GetCustomAttribute<EnumMemberAttribute>();
                    
                    if (enumMemberAttribute?.Value != null)
                    {
                        map[enumMemberAttribute.Value] = (T)enumValue;
                    }
                    else
                    {
                        // Fallback to enum name
                        map[enumValue.ToString()] = (T)enumValue;
                    }
                }

                return map;
            });
        }

        /// <summary>
        /// Gets the mapping from enum values to string values.
        /// </summary>
        private static Dictionary<T, string> GetEnumToStringMap()
        {
            return _enumToStringCache.GetOrAdd(typeof(T), _ =>
            {
                var map = new Dictionary<T, string>();
#pragma warning disable CA2263 // Prefer generic overload when type is known
                var enumValues = Enum.GetValues(typeof(T));
#pragma warning restore CA2263 // Prefer generic overload when type is known

                foreach (var enumValue in enumValues)
                {
                    var field = typeof(T).GetField(enumValue.ToString());
                    var enumMemberAttribute = field?.GetCustomAttribute<EnumMemberAttribute>();
                    
                    if (enumMemberAttribute?.Value != null)
                    {
                        map[(T)enumValue] = enumMemberAttribute.Value;
                    }
                    else
                    {
                        // Fallback to enum name
                        map[(T)enumValue] = enumValue.ToString();
                    }
                }

                return map;
            });
        }
    }

    /// <summary>
    /// Non-generic JSON converter factory for enum types that use EnumMember attributes.
    /// </summary>
    public class JsonStringEnumMemberConverter : JsonConverterFactory
    {
        /// <summary>
        /// Determines if the converter can handle the given type.
        /// </summary>
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum;
        }

        /// <summary>
        /// Creates a converter instance for the specified enum type.
        /// </summary>
        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var converterType = typeof(JsonStringEnumMemberConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }
}
