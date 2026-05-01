// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    /// <summary>
    /// This handles serializing a Dictionary&lt;string, object> with type information.
    /// </summary>
    internal class DictionaryOfObjectConverter : JsonConverter<IDictionary<string, object>>
    {
        public override IDictionary<string, object>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            var value = new Dictionary<string, object>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return value;
                }

                string keyString = reader.GetString();
                var itemValue = JsonSerializer.Deserialize<JsonNode>(ref reader, options);

                if (itemValue is JsonObject jObj && jObj.GetTypeInfo(out var type))
                {
                    var obj = JsonSerializer.Deserialize(itemValue, type, options);

#if NETSTANDARD
                    var changes = new List<KeyValuePair<string, object>>();
#endif

                    // Update dictionary elements with actual type
                    if (obj is IDictionary<string, object> dict)
                    {
                        dict.RemoveTypeInfo();

                        foreach (KeyValuePair<string, object> child in dict)
                        {
                            var childObj = child.Value;

                            if (childObj is JsonElement element && element.ValueKind == JsonValueKind.Object)
                            {
                                childObj = JsonObject.Create(element);
                            }

                            if (childObj is JsonObject typedChild)
                            {
                                if (typedChild.GetTypeInfo(out var childType))
                                {
                                    typedChild.RemoveTypeInfo();
#if NETSTANDARD
                                    changes.Add(new KeyValuePair<string, object>(child.Key, JsonSerializer.Deserialize(typedChild, childType, options)));
#else
                                    dict[child.Key] = JsonSerializer.Deserialize(typedChild, childType, options);
#endif
                                }
                            }
                            else if (childObj is JsonElement valValue)
                            {
                                switch (valValue.ValueKind)
                                {
                                    case JsonValueKind.Number:
#if NETSTANDARD
                                        changes.Add(new KeyValuePair<string, object>(child.Key, valValue.GetInt32()));
#else
                                        dict[child.Key] = valValue.GetInt32();
#endif
                                        break;

                                    case JsonValueKind.String:
#if NETSTANDARD
                                        changes.Add(new KeyValuePair<string, object>(child.Key, valValue.GetString()));
#else
                                        dict[child.Key] = valValue.GetString();
#endif
                                        break;

                                    case JsonValueKind.True:
                                    case JsonValueKind.False:
#if NETSTANDARD
                                        changes.Add(new KeyValuePair<string, object>(child.Key, valValue.GetBoolean()));
#else
                                        dict[child.Key] = valValue.GetBoolean();
#endif
                                        break;
                                }
                            }
                        }
#if NETSTANDARD
                        foreach (var change in changes)
                        {
                            dict[change.Key] = change.Value;
                        }
#endif
                    }
                    value.Add(keyString, obj);
                }
                else if (itemValue is JsonArray jArray)
                {
                    value.Add(keyString, DeserializeJsonArray(jArray, options));
                }
                else
                {
                    value.Add(keyString, DeserializeJsonValue(itemValue));
                }
            }
            throw new JsonException($"JSON did not contain the end of {typeToConvert.FullName}!");
        }

        public override void Write(Utf8JsonWriter writer, IDictionary<string, object> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, object> item in value)
            {
                var newValue = item.Value;
                if (item.Value == null)
                {
                    writer.WriteNull(item.Key);
                    continue;
                }

                writer.WritePropertyName(item.Key);

                if (Convert.GetTypeCode(item.Value) == TypeCode.Object)
                {
                    newValue = JsonSerializer.SerializeToNode(item.Value, options);
                    if (newValue is JsonObject jObj)
                    {
                        SerializeJsonObject(jObj, item.Value, options);
                    }
                    else if (newValue is JsonArray jArray)
                    {
                        SerializeJsonArray(jArray, (IList)item.Value);
                    }
                }

                var element = JsonSerializer.SerializeToElement(newValue, options);
                element.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        private static void SerializeJsonObject(JsonObject jObj, object value, JsonSerializerOptions options)
        {
            jObj.AddTypeInfo(value);

            // Update dictionary elements with type info
            if (value is IDictionary<string, object> children)
            {
                foreach (KeyValuePair<string, object> child in children)
                {
                    var newChildValue = JsonSerializer.SerializeToNode(child.Value, options);
                    if (newChildValue is JsonObject childJObj)
                    {
                        childJObj.AddTypeInfo(child.Value);
                        children[child.Key] = childJObj;
                    }
                }
            }
        }

        private static object DeserializeJsonValue(JsonNode itemValue)
        {
            if (itemValue == null)
            {
                return null;
            }

            object objValue = null;
            var valValue = itemValue.AsValue();
            switch (valValue.GetValueKind())
            {
                case JsonValueKind.Number:
                    if (valValue.TryGetValue<int>(out var intValue))
                    {
                        objValue = intValue;
                    }
                    break;

                case JsonValueKind.String:
                    if (valValue.TryGetValue<DateTime>(out var dateValue))
                    {
                        objValue = dateValue;
                    }
                    else if (valValue.TryGetValue<string>(out var strValue))
                    {
                        objValue = strValue;
                    }
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (valValue.TryGetValue<bool>(out var boolValue))
                    {
                        objValue = boolValue;
                    }
                    break;

                default:
                    objValue = itemValue; 
                    break;

            }

            return objValue;
        }

        private static void SerializeJsonArray(JsonArray jArray, IList sourceList)
        {
            for (int i = 0; i < jArray.Count; i++)
            {
                jArray[i].AddTypeInfo(sourceList[i]);
                if (i == 0)
                {
                    // storing the array type in the first element
                    jArray[i].AddCollectionTypeInfo(sourceList.GetType());
                }
            }
        }

        private static object DeserializeJsonArray(JsonArray jArray, JsonSerializerOptions options)
        {
            IList objValue = null;
            bool isArray = false;
            for (int i = 0; i < jArray.Count; i++)
            {
                var aItem = jArray[i];
                if (aItem.GetTypeInfo(out var childType))
                {
                    if (i == 0)
                    {
                        var collectionType = jArray.GetCollectionTypeInfo();
                        if (collectionType.BaseType == typeof(Array))
                        {
                            var dataType = new Type[] { collectionType.GetElementType() };
                            var genericBase = typeof(List<>);
                            var combinedType = genericBase.MakeGenericType(dataType);
                            objValue = (IList)Activator.CreateInstance(combinedType);
                            isArray = true;
                        }
                        else
                        {
                            objValue = (IList)Activator.CreateInstance(collectionType);
                        }

                        aItem.RemoveCollectionTypeInfo();
                    }

                    aItem.RemoveTypeInfo();

                    objValue.Add(JsonSerializer.Deserialize(aItem, childType, options));
                }
                else
                {
                    objValue.Add(aItem);
                }
            }

            if (objValue != null && isArray)
            {
                var array = Array.CreateInstance(objValue.GetType().GenericTypeArguments[0], objValue.Count);
                for (int i = 0; i < objValue.Count; i++)
                {
                    array.SetValue(objValue[i], i);
                }
                return array;
            }

            return objValue;
        }
    }
}
