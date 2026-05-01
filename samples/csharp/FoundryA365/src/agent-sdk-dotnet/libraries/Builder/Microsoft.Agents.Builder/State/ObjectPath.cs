// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Builder.State
{
    /// <summary>
    /// Helper methods for working with dynamic json objects.
    /// </summary>
    internal static class ObjectPath
    {
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
        };

        /// <summary>
        /// Does an object have a subpath.
        /// </summary>
        /// <param name="obj">object.</param>
        /// <param name="path">path to evaluate.</param>
        /// <returns>true if the path is there.</returns>
        public static bool HasValue(object obj, string path)
        {
            return TryGetPathValue<object>(obj, path, out var value);
        }

        /// <summary>
        /// Get the value for a path relative to an object.
        /// </summary>
        /// <typeparam name="T">type to return.</typeparam>
        /// <param name="obj">object to start with.</param>
        /// <param name="path">path to evaluate.</param>
        /// <param name="set">Set path value with result if true.</param>
        /// <returns>value or default(T).</returns>
        public static T GetPathValue<T>(object obj, string path, bool set = false)
        {
            if (TryGetPathValue<T>(obj, path, out var value, set))
            {
                return value;
            }

            throw new KeyNotFoundException(path);
        }

        /// <summary>
        /// Get the value for a path relative to an object.
        /// </summary>
        /// <typeparam name="T">type to return.</typeparam>
        /// <param name="obj">object to start with.</param>
        /// <param name="path">path to evaluate.</param>
        /// <param name="defaultValue">default value to use if any part of the path is missing.</param>
        /// <param name="set">Set path value with result if true.</param>
        /// <returns>value or default(T).</returns>
        public static T GetPathValue<T>(object obj, string path, T defaultValue, bool set = false)
        {
            if (TryGetPathValue<T>(obj, path, out var value, set))
            {
                return value;
            }

            return defaultValue;
        }

        /// <summary>
        /// Get the value for a path relative to an object.
        /// </summary>
        /// <typeparam name="T">type to return.</typeparam>
        /// <param name="obj">object to start with.</param>
        /// <param name="path">path to evaluate.</param>
        /// <param name="value">value for the path.</param>
        /// <param name="convertedSet">If the value was converted, set the path value with the converted value.</param>
        /// <returns>true if successful.</returns>
        public static bool TryGetPathValue<T>(object obj, string path, out T value, bool convertedSet = false)
        {
            value = default;

            if (obj == null)
            {
                return false;
            }

            if (path == null)
            {
                return false;
            }

            if (path.Length == 0)
            {
                value = MapValueTo<T>(obj);
                return true;
            }

            if (!TryResolvePath(obj, path, out var segments))
            {
                return false;
            }

            if (!ResolveSegments(obj, segments, out var result))
            {
                return false;
            }

            // look to see if it's ExpressionProperty and bind it if it is
            // NOTE: this bit of duck typing keeps us from adding dependency between adaptiveExpressions and Dialogs.
            if (result.GetType().GetProperty("ExpressionText") != null)
            {
                var method = result.GetType().GetMethod("GetValue", new[] { typeof(object) });
                if (method != null)
                {
                    result = method.Invoke(result, new[] { obj });
                }
            }

            try
            {
                value = MapValueTo<T>(result);
                if (convertedSet && value.GetType() != result.GetType())
                {
                    SetPathValueInner(segments, obj, value, false);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Given an object evaluate a path to set the value.
        /// </summary>
        /// <param name="obj">object to start with.</param>
        /// <param name="path">path to evaluate.</param>
        /// <param name="value">value to store.</param>
        /// <param name="json">if true, sets the value as primitive JSON objects.</param>
        public static void SetPathValue(object obj, string path, object value, bool json = true)
        {
            if (!TryResolvePath(obj, path, out var segments))
            {
                return;
            }

            SetPathValueInner(segments, obj, value, json);
        }

        private static void SetPathValueInner(List<object> segments, object obj, object value, bool json = true)
        {
            dynamic current = obj;
            for (var i = 0; i < segments.Count - 1; i++)
            {
                var segment = segments[i];
                dynamic next;
                if (segment is int index)
                {
                    next = current[index];
                    if (next == null)
                    {
                        if (((ICollection)current).Count <= index)
                        {
                            // Expand array to index
                            for (var idx = ((ICollection)current).Count; idx <= index; ++idx)
                            {
                                ((JsonArray)current)[idx] = null;
                            }

                            next = current[index];
                        }
                    }
                }
                else
                {
                    var strSegment = segment as string;
                    next = GetObjectProperty(current, strSegment);
                    if (next == null)
                    {
                        // Create object or array base on next segment
                        var nextSegment = segments[i + 1];
                        if (nextSegment is string)
                        {
                            SetObjectSegment(current, strSegment, new JsonObject(), json);
                            next = GetObjectProperty(current, strSegment);
                        }
                        else
                        {
                            SetObjectSegment(current, strSegment, new JsonArray(), json);
                            next = GetObjectProperty(current, strSegment);
                        }
                    }
                }

                current = next;
            }

            var lastSegment = segments.Last();
            SetObjectSegment(current, lastSegment, value, json);
        }

        /// <summary>
        /// Remove path from object.
        /// </summary>
        /// <param name="obj">Object to change.</param>
        /// <param name="path">Path to remove.</param>
        public static void RemovePathValue(object obj, string path)
        {
            if (!TryResolvePath(obj, path, out var segments))
            {
                return;
            }

            dynamic current = obj;
            for (var i = 0; i < segments.Count - 1; i++)
            {
                var segment = segments[i];
                if (!ResolveSegment(ref current, segment))
                {
                    return;
                }
            }

            if (current != null)
            {
                var lastSegment = segments.Last();
                if (lastSegment is string property)
                {
                    // ConcurrentDictionary doesn't implement Remove, but it does implement IDictionary
                    if (current is IDictionary<string, object> dict)
                    {
                        dict.Remove(property);
                    }
                    else
                    {
                        current.Remove(property);
                    }
                }
                else
                {
                    current[(int)lastSegment] = null;
                }
            }
        }

        /// <summary>
        /// Apply an action to all properties in an object.
        /// </summary>
        /// <param name="obj">Object to map against.</param>
        /// <param name="action">Action to take.</param>
        public static void ForEachProperty(object obj, Action<string, object> action)
        {
            if (obj is IDictionary<string, object> dict)
            {
                foreach (var entry in dict)
                {
                    action(entry.Key, entry.Value);
                }
            }
            else if (obj is JsonObject jobj)
            {
                foreach (var property in jobj)
                {
                    action(property.Key, property.Value);
                }
            }

            /* For tracking purposes, only use pure dictionary/jobject.
            else if (!(obj.GetType().IsPrimitive || obj.GetType().IsArray() || obj is string || obj is DateTime || obj is DateTimeOffset || obj is JValue || obj is JArray))
            {
                foreach (var property in obj.GetType().GetProperties())
                {
                    // Check for indexer
                    if (property.GetIndexParameters().Length == 0)
                    {
                        action(property.Name, property.GetValue(obj));
                    }
                }
            }
            */
        }

        /// <summary>
        /// Get all properties in an object.
        /// </summary>
        /// <param name="obj">Object to enumerate property names.</param>
        /// <returns>enumeration of property names on the object if it is not a value type.</returns>
        public static IEnumerable<string> GetProperties(object obj)
        {
            if (obj == null)
            {
            }
            else if (obj is IDictionary<string, object> dict)
            {
                foreach (var entry in dict)
                {
                    yield return entry.Key;
                }
            }
            else if (obj is JsonObject jobj)
            {
                foreach (var property in jobj)
                {
                    yield return property.Key;
                }
            }
            else
            {
                foreach (var property in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(p => p.Name))
                {
                    yield return property;
                }
            }
        }

        /// <summary>
        /// Detects if property exists on object.
        /// </summary>
        /// <param name="obj">object.</param>
        /// <param name="name">name of the property.</param>
        /// <returns>true if found.</returns>
        public static bool ContainsProperty(object obj, string name)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj is IDictionary<string, object> dict)
            {
                return dict.ContainsKey(name);
            }

            if (obj is JsonObject jobj)
            {
                return jobj.ContainsKey(name);
            }

            return obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Any(property => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Clone an object.
        /// </summary>
        /// <typeparam name="T">Type to clone.</typeparam>
        /// <param name="obj">The object.</param>
        /// <returns>The object as Json.</returns>
        public static T Clone<T>(T obj)
        {
            return ProtocolJsonSerializer.CloneTo<T>(obj);
        }

        /// <summary>
        /// Equivalent to javascripts ObjectPath.Assign, creates a new object from startObject overlaying any non-null values from the overlay object.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="startObject">Intial object.</param>
        /// <param name="overlayObject">Overlay object.</param>
        /// <returns>merged object.</returns>
        public static T Merge<T>(T startObject, T overlayObject)
            where T : class
        {
            return Assign<T>(startObject, overlayObject);
        }

        /// <summary>
        /// Equivalent to javascripts ObjectPath.Assign, creates a new object from startObject overlaying any non-null values from the overlay object.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="startObject">intial object of any type.</param>
        /// <param name="overlayObject">overlay object of any type.</param>
        /// <returns>merged object.</returns>
        public static T Assign<T>(object startObject, object overlayObject)
            where T : class
        {
            return (T)Assign(startObject, overlayObject, typeof(T));
        }

        /// <summary>
        /// Equivalent to javascripts ObjectPath.Assign, creates a new object from startObject overlaying any non-null values from the overlay object.
        /// </summary>
        /// <param name="startObject">intial object of any type.</param>
        /// <param name="overlayObject">overlay object of any type.</param>
        /// <param name="type">type to output.</param>
        /// <returns>merged object.</returns>
        public static object Assign(object startObject, object overlayObject, Type type)
        {
            if (startObject != null && overlayObject != null)
            {
                // make a deep clone JObject of the startObject
                var jsMerged = startObject is JsonObject ? (JsonObject)(startObject as JsonObject).DeepClone() : JsonSerializer.SerializeToNode(startObject, _serializerOptions);

                // get a JObject of the overlay object
                var jsOverlay = overlayObject is JsonObject ? overlayObject as JsonObject : JsonSerializer.SerializeToNode(overlayObject, _serializerOptions);

                var merged = ObjectMerge.Merge(jsMerged, jsOverlay);

                /*
                jsMerged.Merge(jsOverlay, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Replace,
                    MergeNullValueHandling = MergeNullValueHandling.Ignore,
                });
                */

                return merged.Deserialize(type, _serializerOptions);
            }

            var singleObject = startObject ?? overlayObject;
            if (singleObject != null)
            {
                if (singleObject is JsonObject)
                {
                    return (singleObject as JsonObject).Deserialize(type);
                }

                return singleObject;
            }

            return (Type)Activator.CreateInstance(type);
        }

        /// <summary>
        /// Convert a generic object to a typed object.
        /// </summary>
        /// <typeparam name="T">type to convert to.</typeparam>
        /// <param name="val">value to convert.</param>
        /// <returns>converted value.</returns>
        public static T MapValueTo<T>(object val)
        {
            if (val is JsonElement)
            {
                T result = ProtocolJsonSerializer.ToObject<T>(val);
                return result;
            }

            if (typeof(T) == typeof(object))
            {
                return (T)val;
            }

            if (val is JsonValue valValue)
            {
                if (valValue.TryGetValue(out T value))
                {
                    return value;
                }

                if (valValue.GetValueKind() == JsonValueKind.Number && typeof(T) == typeof(string))
                {
                    var number = valValue.GetValue<int>();
                    return ProtocolJsonSerializer.ToObject<T>(number.ToString());
                }
                
                if (valValue.GetValueKind() == JsonValueKind.String && typeof(T) == typeof(int))
                {
                    var str = valValue.GetValue<string>();
                    return ProtocolJsonSerializer.ToObject<T>(str);
                }
            }

            if (val is JsonNode valNode)
            {
                return valNode.Deserialize<T>(_serializerOptions);
            }

            if (typeof(T) == typeof(JsonNode))
            {
                return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(val, _serializerOptions), _serializerOptions);
            }

            if (val is T t)
            {
                return t;
            }

            if (val is short || val is int || val is long ||
                val is ushort || val is uint || val is ulong ||
                val is decimal || val is float || val is double)
            {
                return (T)Convert.ChangeType(val, typeof(T));
            }

            var json = JsonSerializer.Serialize(val, _serializerOptions);
            return JsonSerializer.Deserialize<T>(json, _serializerOptions);
        }

        /// <summary>
        /// Given an root object and property path, resolve to a constant if eval = true or a constant path otherwise.  
        /// conversation[user.name][user.age] => ['conversation', 'joe', 32].
        /// </summary>
        /// <param name="obj">root object.</param>
        /// <param name="propertyPath">property path to resolve.</param>
        /// <param name="segments">Path segments.</param>
        /// <param name="eval">True to evaluate resulting segments.</param>
        /// <returns>True if it was able to resolve all nested references.</returns>
        public static bool TryResolvePath(object obj, string propertyPath, out List<object> segments, bool eval = false)
        {
            var soFar = new List<object>();
            segments = soFar;
            var first = propertyPath.Length > 0 ? propertyPath[0] : ' ';
            if (first == '\'' || first == '"')
            {
                if (!propertyPath.EndsWith(first.ToString(), StringComparison.Ordinal))
                {
                    return false;
                }

                soFar.Add(propertyPath.Substring(1, propertyPath.Length - 2));
            }
            else if (int.TryParse(propertyPath, out var number))
            {
                soFar.Add(number);
            }
            else
            {
                var start = 0;
                int i;

                // Emit current fragment
                void Emit()
                {
                    var segment = propertyPath.Substring(start, i - start);
                    if (!string.IsNullOrEmpty(segment))
                    {
                        soFar.Add(segment);
                    }

                    start = i + 1;
                }

                // Scan path evaluating as we go
                for (i = 0; i < propertyPath.Length; ++i)
                {
                    var ch = propertyPath[i];
                    if (ch == '.' || ch == '[')
                    {
                        Emit();
                    }

                    if (ch == '[')
                    {
                        // Bracket expression
                        var nesting = 1;
                        while (++i < propertyPath.Length)
                        {
                            ch = propertyPath[i];
                            if (ch == '[')
                            {
                                ++nesting;
                            }
                            else if (ch == ']')
                            {
                                --nesting;
                                if (nesting == 0)
                                {
                                    break;
                                }
                            }
                        }

                        if (nesting > 0)
                        {
                            // Unbalanced brackets
                            return false;
                        }

                        var expr = propertyPath.Substring(start, i - start);
                        start = i + 1;
                        if (!TryResolvePath(obj, expr, out var indexer, true) || indexer.Count != 1)
                        {
                            // Could not resolve bracket expression
                            return false;
                        }

                        var result = MapValueTo<string>(indexer.First());
                        if (int.TryParse(result, out var index))
                        {
                            soFar.Add(index);
                        }
                        else
                        {
                            soFar.Add(result);
                        }
                    }
                }

                Emit();

                if (eval)
                {
                    if (!ResolveSegments(obj, soFar, out var result))
                    {
                        return false;
                    }

                    soFar.Clear();
                    soFar.Add(MapValueTo<string>(result));
                }
            }

            return true;
        }

        private static bool ResolveSegment(ref dynamic current, object segment)
        {
            if (current != null)
            {
                if (segment is int index)
                {
                    current = current[index];
                }
                else
                {
                    current = GetObjectProperty(current, segment as string);
                }

                // This interprets any null value as not being present.
                return (object)current != null;
            }

            return false;
        }

        private static bool ResolveSegments(dynamic current, List<object> segments, out object result)
        {
            result = current;
            foreach (var segment in segments)
            {
                if (!ResolveSegment(ref result, segment))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get a property or array element from an object.
        /// </summary>
        /// <param name="obj">object.</param>
        /// <param name="property">property or array segment to get relative to the object.</param>
        /// <returns>the value or null if not found.</returns>
        private static object GetObjectProperty(object obj, string property)
        {
            if (obj == null)
            {
                return null;
            }

            if (obj is IDictionary<string, object> dict)
            {
                var key = dict.Keys.Where(key => string.Equals(key, property, StringComparison.OrdinalIgnoreCase)).FirstOrDefault() ?? property;
                if (dict.TryGetValue(key, out var value))
                {
                    return value;
                }

                return null;
            }

            if (obj is JsonObject jobj)
            {
                var key = jobj.AsEnumerable().Where(kvPair => string.Equals(kvPair.Key, property, StringComparison.OrdinalIgnoreCase)).FirstOrDefault().Key ?? property;
                jobj.TryGetPropertyValue(key, out var value);
                return value;
            }

            if (obj is JsonValue jval)
            {
                // in order to make things like "this.value.Length" work, when "this.value" is a string.
                //return GetObjectProperty(JsonSerializer.SerializeToNode(jval), property);
                throw new NotImplementedException();
            }

            var prop = obj.GetType().GetProperties().Where(p => string.Equals(p.Name, property, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (prop != null)
            {
                return prop.GetValue(obj);
            }

            return null;
        }

        /// <summary>
        /// Given an object, set a property or array element on it with a value.
        /// </summary>
        /// <param name="obj">object to modify.</param>
        /// <param name="segment">property or array segment to put the value in.</param>
        /// <param name="value">value to store.</param>
        /// <param name="json">if true, value will be normalized to JSON primitive objects.</param>
        private static void SetObjectSegment(object obj, object segment, object value, bool json = true)
        {
            object val;

            val = GetNormalizedValue(value, json);
            if (segment is int index)
            {
                if (obj is JsonArray jarray)
                {
                    // grow array if required
                    var jar = obj as JsonArray;
                    for (var i = jar.Count; i <= index; i++)
                    {
                        jar.Add(null);
                    }

                    jar[index] = JsonSerializer.SerializeToNode(val);
                }
                else if (obj is Array array)
                {
                    if (index >= array.Length)
                    {
                        // TODO
                        throw new ArgumentException("Cannot grow arrays yet");
                    }

                    array.SetValue(value, index);
                }
                else if (obj is IList list)
                {
                    if (index >= list.Count)
                    {
                        for (var i = list.Count; i <= index; i++)
                        {
                            list.Add(null);
                        }
                    }

                    list[index] = value;
                }
                return;
            }

            var property = segment as string;
            if (obj is IDictionary<string, object> dict)
            {
                // For case insensitive key
                var key = dict.Keys.Where(k => string.Equals(k, property, StringComparison.OrdinalIgnoreCase)).FirstOrDefault() ?? property;
                dict[key] = value;
                return;
            }

            if (obj is JsonObject jobj)
            {
                // For case insensitive key
                var key = jobj.Where(p => string.Equals(p.Key, property, StringComparison.OrdinalIgnoreCase)).FirstOrDefault().Key ?? property;
                jobj[key] = val is JsonNode valNode ? valNode : JsonSerializer.SerializeToNode(val);
                return;
            }

            var prop = obj.GetType().GetProperty(property);
            if (prop != null)
            {
                if (prop.GetValue(obj) != value)
                {
                    prop.SetValue(obj, value);
                }
            }
        }

        /// <summary>
        /// Normalize value as json objects.
        /// </summary>
        /// <param name="value">value to normalize.</param>
        /// <param name="json">normalize as json objects.</param>
        /// <returns>normalized value.</returns>
        private static object GetNormalizedValue(object value, bool json)
        {
            object val;
            if (json)
            {
                if (value is JsonNode node)
                {
                    val = node.DeepClone();
                }
                else if (value is JsonElement)
                {
                    val = JsonSerializer.SerializeToNode(value, _serializerOptions);
                }
                else if (value == null)
                {
                    val = null;
                }
                else if (value is string || value is byte || value is bool ||
                         value is DateTime || value is DateTimeOffset ||
                         value is short || value is int || value is long ||
                         value is ushort || value is uint || value is ulong ||
                         value is decimal || value is float || value is double)
                {
                    val = JsonValue.Create(value);
                }
                else
                {
                    var jsonValue = JsonSerializer.Serialize(value, _serializerOptions);
                    val = JsonObject.Parse(jsonValue, new JsonNodeOptions() { PropertyNameCaseInsensitive = true });
                }
            }
            else
            {
                val = value;
            }

            return val;
        }
    }
}
