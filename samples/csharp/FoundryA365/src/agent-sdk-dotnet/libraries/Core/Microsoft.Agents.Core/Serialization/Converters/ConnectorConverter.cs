// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    public abstract class ConnectorConverter<T> : JsonConverter<T> where T : new()
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();
        private static readonly ConcurrentDictionary<(Type, bool, Type), Dictionary<string, (PropertyInfo, bool)>> JsonPropertyMetadataCache = new();

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"JSON is not at the start of {typeToConvert.FullName}!");
            }

            if (typeToConvert.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new JsonException($"Type '{typeToConvert.FullName}' must have a public parameterless constructor to be deserialized.");
            }
            var value = Activator.CreateInstance(typeToConvert);

            var propertyMetadataMap = GetJsonPropertyMetadata(typeToConvert, options.PropertyNameCaseInsensitive, options.PropertyNamingPolicy);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return (T)value;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();

                    if (propertyMetadataMap.TryGetValue(propertyName, out var entry))
                    {
                        ReadProperty(ref reader, (T)value, propertyName, options, entry.Property);
                    }
                    else
                    {
                        ReadExtensionData(ref reader, (T)value, propertyName, options);
                    }
                }
            }

            throw new JsonException($"JSON did not contain the end of {typeToConvert.FullName}!");
        }
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            var type = value.GetType();
            var properties = GetCachedProperties(type);
            var propertyMetadataMap = GetJsonPropertyMetadata(type, false, options.PropertyNamingPolicy); // case-insensitivity doesn’t matter here
            var reverseMap = propertyMetadataMap.ToDictionary(kv => kv.Value.Property, kv => (kv.Key, kv.Value.IsIgnored));

            foreach (var property in properties)
            {
                if (!reverseMap.TryGetValue(property, out var propertyMetadata) || propertyMetadata.IsIgnored)
                {
                    continue;
                }

                if (property.Name == "Properties" && typeof(IDictionary<string, JsonElement>).IsAssignableFrom(property.PropertyType))
                {
                    IDictionary<string, JsonElement> propertyValue = (IDictionary<string, JsonElement>) property.GetValue(value);
                    if (propertyValue.Count == 0)
                    {
                        continue;
                    }
                }

                if (!TryWriteExtensionData(writer, value, property.Name))
                {
                    var propertyValue = property.GetValue(value);

#if SKIP_EMPTY_LISTS
                    if (propertyValue is IList list)
                    {
                        if (list == null || list.Count == 0)
                        {
                            continue;
                        }
                    }
#endif
                    if (propertyValue != null || !(options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull))

                    {
                        var propertyName = propertyMetadata.Key ?? property.Name;

                        writer.WritePropertyName(propertyName);

                        if (property.PropertyType == typeof(object) && propertyValue is string s)
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
                            var json = System.Text.Json.JsonSerializer.Serialize(propertyValue, propertyValue?.GetType() ?? property.PropertyType, options);
                            JsonDocument.Parse(json).WriteTo(writer);
                        }
                    }
                }
            }

            writer.WriteEndObject();
        }

        protected virtual bool TryReadCollectionProperty(ref Utf8JsonReader reader, T value, string propertyName, JsonSerializerOptions options)
        {
            return false;
        }

        protected virtual bool TryReadGenericProperty(ref Utf8JsonReader reader, T value, string propertyName, JsonSerializerOptions options)
        {
            return false;
        }

        protected virtual void ReadExtensionData(ref Utf8JsonReader reader, T value, string propertyName, JsonSerializerOptions options)
        {
        }

        /// <summary>
        /// Handle undefined properties in JSON without the need for the JsonExtensionData annotation.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <param name="options"></param>
        protected virtual bool TryReadExtensionData(ref Utf8JsonReader reader, T value, string propertyName, JsonSerializerOptions options)
        {
            return false;
        }

        /// <summary>
        /// Handle undefined properties in JSON without the need for the JsonExtensionData annotation.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <param name="writer"></param>
        /// <param name="options"></param>
        protected virtual bool TryWriteExtensionData(Utf8JsonWriter writer, T value, string propertyName)
        {
            return false;
        }

        protected void SetCollection<TCollection>(ref Utf8JsonReader reader, IList<TCollection> collection, JsonSerializerOptions options)
        {
            collection.Clear();

            var items = System.Text.Json.JsonSerializer.Deserialize<IList<TCollection>>(ref reader, options);

            if (items != null)
            {
                foreach (var item in items)
                {
                    collection.Add(item);
                }
            }
        }

        /// <summary>
        /// This is to handle 'object' type properties that must conform to the original BF SDK handling.
        /// A simple type (string, int, etc...) is set as the value.  Complex objects are of type 'JsonElement'.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="setter"></param>
        /// <param name="options"></param>
        protected void SetGenericProperty(ref Utf8JsonReader reader, Action<object> setter, JsonSerializerOptions options)
        {
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<object>(ref reader, options);

            if (deserialized == null)
            {
                return;
            }

            if (deserialized is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    if (!ProtocolJsonSerializer.UnpackObjectStrings)
                    {
                        var json = element.GetString();
                        setter(json);
                    }
                    else
                    {
                        try
                        {
                            var json = element.GetString();

                            // Check if the underlying JSON is a reference type
                            using (var document = JsonDocument.Parse(json))
                            {
                                setter(document.RootElement.Clone());
                                if (document.RootElement.ValueKind == JsonValueKind.Object || document.RootElement.ValueKind == JsonValueKind.Array)
                                {
                                    setter(document.RootElement.Clone());
                                }
                                else
                                {
                                    setter(element.GetString());
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // JSON is a value type
                            setter(element.GetString());
                        }
                    }
                }
                else if (element.ValueKind == JsonValueKind.Number)
                {
                    setter(element.GetInt32());
                }
                else if (element.ValueKind == JsonValueKind.True)
                {
                    setter(true);
                }
                else if (element.ValueKind == JsonValueKind.False)
                {
                    setter(false);
                }
                else
                {
                    setter(element);
                }

                return;
            }

            setter(deserialized);
        }

        protected virtual void ReadProperty(ref Utf8JsonReader reader, T value, string propertyName, JsonSerializerOptions options, PropertyInfo property)
        {
            if (TryReadExtensionData(ref reader, value, property.Name, options))
            {
                return;
            }

            if (TryReadCollectionProperty(ref reader, value, property.Name, options))
            {
                var CollectionPropertyValue = System.Text.Json.JsonSerializer.Deserialize(ref reader, property.PropertyType, options);
                if (CollectionPropertyValue is IList prospectiveList)
                {
#if SKIP_EMPTY_LISTS
                    if (prospectiveList.Count != 0)
                    {
                        property.SetValue(value, CollectionPropertyValue);
                    }
                    else
                    {
                        property.SetValue(value, null);
                    }
#else
                    property.SetValue(value, CollectionPropertyValue);
#endif
                }
                return;
            }

            if (TryReadGenericProperty(ref reader, value, property.Name, options))
            {
                return;
            }

            var propertyValue = System.Text.Json.JsonSerializer.Deserialize(ref reader, property.PropertyType, options);
            property.SetValue(value, propertyValue);
        }

        private static PropertyInfo[] GetCachedProperties(Type type)
        {
            return PropertyCache.GetOrAdd(type, static t => t.GetProperties());
        }

        private static Dictionary<string, (PropertyInfo Property, bool IsIgnored)> GetJsonPropertyMetadata(Type type, bool caseInsensitive, JsonNamingPolicy? namingPolicy)
        {
            var cacheKey = (type, caseInsensitive, namingPolicy?.GetType());
            return JsonPropertyMetadataCache.GetOrAdd(cacheKey, key =>
            {
                var (t, insensitive, _) = key;
                var comparer = insensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                var metadata  = new Dictionary<string, (PropertyInfo, bool)>(comparer);

                foreach (var prop in GetCachedProperties(t))
                {
                    var resolvedName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                        ?? namingPolicy?.ConvertName(prop.Name)
                        ?? prop.Name;

                    if (metadata.ContainsKey(resolvedName))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate JSON property name detected: '{resolvedName}' maps to multiple properties in type '{t.FullName}'."
                        );
                    }

                    metadata [resolvedName] = (prop, prop.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition == JsonIgnoreCondition.Always);
                }

                return metadata;
            });
        }
    }
}
