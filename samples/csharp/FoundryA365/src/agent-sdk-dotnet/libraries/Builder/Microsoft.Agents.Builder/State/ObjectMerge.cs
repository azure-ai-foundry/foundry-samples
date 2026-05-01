// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.Agents.Builder.State
{
    internal class ObjectMerge
    {
        public static JsonNode Merge(JsonNode originalNode, JsonNode newNode)
        {
            JsonElement originalElement = JsonSerializer.SerializeToElement(originalNode);
            JsonElement newElement = JsonSerializer.SerializeToElement(newNode);

            if (originalElement.ValueKind != JsonValueKind.Array && originalElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"The original JSON to merge new content into must be a container type. Instead it is {originalElement.ValueKind}.");
            }

#if !NETSTANDARD
            var outputBuffer = new ArrayBufferWriter<byte>();
#else
            var outputBuffer = new LocalArrayBufferWriter<byte>(256);
#endif
            using (var jsonWriter = new Utf8JsonWriter(outputBuffer, new JsonWriterOptions { Indented = true }))
            {
                if (originalElement.ValueKind != newElement.ValueKind)
                {
                    return originalNode;
                }

                if (originalElement.ValueKind == JsonValueKind.Array)
                {
                    MergeArrays(jsonWriter, originalElement, newElement);
                }
                else
                {
                    MergeObjects(jsonWriter, originalElement, newElement);
                }
            }
#if !NETSTANDARD
            return JsonSerializer.Deserialize<JsonNode>(Encoding.UTF8.GetString(outputBuffer.WrittenSpan));
#else
            return JsonSerializer.Deserialize<JsonNode>(Encoding.UTF8.GetString(outputBuffer.WrittenSpan.ToArray()));
#endif
        }

        private static void MergeObjects(Utf8JsonWriter jsonWriter, JsonElement root1, JsonElement root2)
        {
            Debug.Assert(root1.ValueKind == JsonValueKind.Object);
            Debug.Assert(root2.ValueKind == JsonValueKind.Object);

            jsonWriter.WriteStartObject();

            // Write all the properties of the first document.
            // If a property exists in both documents, either:
            // * Merge them, if the value kinds match (e.g. both are objects or arrays),
            // * Completely override the value of the first with the one from the second, if the value kind mismatches (e.g. one is object, while the other is an array or string),
            // * Or favor the value of the first (regardless of what it may be), if the second one is null (i.e. don't override the first).
            foreach (JsonProperty property in root1.EnumerateObject())
            {
                string propertyName = property.Name;

                JsonValueKind newValueKind;

                if (root2.TryGetProperty(propertyName, out JsonElement newValue) && (newValueKind = newValue.ValueKind) != JsonValueKind.Null)
                {
                    jsonWriter.WritePropertyName(propertyName);

                    JsonElement originalValue = property.Value;
                    JsonValueKind originalValueKind = originalValue.ValueKind;

                    if (newValueKind == JsonValueKind.Object && originalValueKind == JsonValueKind.Object)
                    {
                        MergeObjects(jsonWriter, originalValue, newValue); // Recursive call
                    }
                    else if (newValueKind == JsonValueKind.Array && originalValueKind == JsonValueKind.Array)
                    {
                        MergeArrays(jsonWriter, originalValue, newValue);
                    }
                    else
                    {
                        newValue.WriteTo(jsonWriter);
                    }
                }
                else
                {
                    property.WriteTo(jsonWriter);
                }
            }

            // Write all the properties of the second document that are unique to it.
            foreach (JsonProperty property in root2.EnumerateObject())
            {
                if (!root1.TryGetProperty(property.Name, out _))
                {
                    property.WriteTo(jsonWriter);
                }
            }

            jsonWriter.WriteEndObject();
        }

        private static void MergeArrays(Utf8JsonWriter jsonWriter, JsonElement root1, JsonElement root2)
        {
            Debug.Assert(root1.ValueKind == JsonValueKind.Array);
            Debug.Assert(root2.ValueKind == JsonValueKind.Array);

            jsonWriter.WriteStartArray();

            // Write all the elements from both JSON arrays
            foreach (JsonElement element in root1.EnumerateArray())
            {
                element.WriteTo(jsonWriter);
            }
            foreach (JsonElement element in root2.EnumerateArray())
            {
                element.WriteTo(jsonWriter);
            }

            jsonWriter.WriteEndArray();
        }
    }
}
