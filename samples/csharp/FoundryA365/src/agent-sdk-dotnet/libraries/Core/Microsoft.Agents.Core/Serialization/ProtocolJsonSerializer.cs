// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization.Converters;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Extensions for converting objects to desired types using serialization.
    /// </summary>
    public static class ProtocolJsonSerializer
    {
        public const string ApplicationJson = "application/json";
        public static JsonSerializerOptions SerializationOptions { get; private set; } = InitSerializerOptions();
        public static bool UnpackObjectStrings { get; set; } = true;

        /// <summary>
        /// Provides a way to turn off the {channelId}:{product} notation.  If false,
        /// ChannelId.ToString() is just the {channelId} value.  However, serialization of the 
        /// ProductInfo Entity is still accounted for.  ChannelId.SubChannel is still populated
        /// with the ProductInfo.Id value in any case.
        /// It is not recommended to set false without guidance.
        /// </summary>
        public static bool ChannelIdIncludesProduct { get; set; } = true;

        /// <summary>
        /// Maintains a mapping of entity type names to their corresponding Type objects.
        /// </summary>
        public static ConcurrentDictionary<string, Type> EntityTypes { get; private set; } = CoreEntities();

        private static readonly object _optionsLock = new object();

        static ProtocolJsonSerializer()
        {
            SerializationInitAssemblyAttribute.InitSerialization();
            EntityInitAssemblyAttribute.InitSerialization();
        }

        private static JsonSerializerOptions InitSerializerOptions()
        {
            var options = new JsonSerializerOptions()
                .ApplyCoreOptions();

            options.TypeInfoResolver = JsonTypeInfoResolver.Combine(
                CoreJsonContext.Default,
                new DefaultJsonTypeInfoResolver());

            return options;
        }

        private static ConcurrentDictionary<string, Type> CoreEntities()
        {
            var entities = new ConcurrentDictionary<string, Type>();
            entities[Models.EntityTypes.ActivityTreatment] = typeof(ActivityTreatment);
            entities[Models.EntityTypes.AICitation] = typeof(AIEntity);
            entities[Models.EntityTypes.GeoCoordinates] = typeof(GeoCoordinates);
            entities[Models.EntityTypes.Mention] = typeof(Mention);
            entities[Models.EntityTypes.Place] = typeof(Place);
            entities[Models.EntityTypes.ProductInfo] = typeof(ProductInfo);
            entities[Models.EntityTypes.StreamInfo] = typeof(StreamInfo);
            entities[Models.EntityTypes.Thing] = typeof(Thing);
            return entities;
        }

        public static void ApplyExtensionConverters(IList<JsonConverter> extensionConverters)
        {
            lock (_optionsLock)
            {
                var newOptions = SerializationOptions;
                if (newOptions.IsReadOnly)
                {
                    newOptions = new JsonSerializerOptions(SerializationOptions);
                }

                foreach (var converter in extensionConverters)
                {
                    newOptions.Converters.Add(converter);
                }

                SerializationOptions = newOptions;
            }
        }

        /// <summary>
        /// Applies a transformation function to <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.SerializationOptions"/>, replacing it with
        /// the result. This is an advanced escape hatch — prefer <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.ApplyExtensionConverters"/>
        /// or <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.AddTypeInfoResolver"/> for typical extensions.
        /// </summary>
        /// <param name="applyFunc">
        /// A function that receives the current options and returns the new options.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>Important:</b> If your function replaces <see cref="System.Text.Json.JsonSerializerOptions.TypeInfoResolver"/>,
        /// you must include <c>CoreJsonContext.Default</c> in the new resolver chain.
        /// Omitting it silently removes source-generated metadata for all core model types.
        /// Use <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver[])"/> to chain resolvers:
        /// <code>
        /// options.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        ///     YourContext.Default,
        ///     CoreJsonContext.Default,
        ///     new DefaultJsonTypeInfoResolver());
        /// </code>
        /// </para>
        /// </remarks>
        public static void ApplyExtensionOptions(Func<JsonSerializerOptions, JsonSerializerOptions> applyFunc)
        {
            lock (_optionsLock)
            {
                var newOptions = SerializationOptions;
                if (newOptions.IsReadOnly)
                {
                    newOptions = new JsonSerializerOptions(SerializationOptions);
                }

                SerializationOptions = applyFunc(newOptions);
            }
        }

        /// <summary>
        /// Prepends a <see cref="System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver"/> (e.g., a source-generated
        /// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>) to the resolver chain
        /// used by <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.SerializationOptions"/>. The resolver is consulted before any previously
        /// registered resolvers and before the reflection fallback.
        /// Call from a <see cref="Microsoft.Agents.Core.Serialization.SerializationInitAssemblyAttribute"/>-decorated <c>Init()</c> method.
        /// </summary>
        /// <remarks>
        /// Each call prepends the new resolver at the front of the chain.
        /// <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver[])"/> returns the first non-null result in order,
        /// so the most-recently-added resolver wins for any given type.
        /// </remarks>
        public static void AddTypeInfoResolver(IJsonTypeInfoResolver resolver)
        {
            lock (_optionsLock)
            {
                var newOptions = SerializationOptions;
                if (newOptions.IsReadOnly)
                {
                    newOptions = new JsonSerializerOptions(SerializationOptions);
                }

                newOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
                    resolver,
                    newOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver());

                SerializationOptions = newOptions;
            }
        }

        public static void AddEntityType(string entityTypeName, Type entityType)
        {
            EntityTypes[entityTypeName] = entityType;
        }

        private static JsonSerializerOptions ApplyCoreOptions(this JsonSerializerOptions options)
        {
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
            options.IncludeFields = true;
            options.NumberHandling = JsonNumberHandling.AllowReadingFromString;

            options.Converters.Add(new ActivityConverter());
            options.Converters.Add(new IActivityConverter());
            //options.Converters.Add(new ObjectTypeConverter());
            options.Converters.Add(new EntityConverter());

            // Move to Dialogs
            options.Converters.Add(new Array2DConverter());
            options.Converters.Add(new DictionaryOfObjectConverter());

            return options;
        }

        /// <summary>
        /// Object to JsonElement conversion.
        /// </summary>
        /// <param name="value">The object to convert to a <see cref="System.Text.Json.JsonElement"/>.</param>
        /// <returns>A <see cref="System.Text.Json.JsonElement"/> representing the specified object.</returns>
        public static JsonElement ToJsonElement(this object value)
        {
            return ToObject<JsonElement>(value);
        }

        /// <summary>
        /// Decompose an object into its constituent JSON elements.
        /// </summary>
        /// <param name="value">The object to be decomposed into JSON elements.</param>
        /// <returns>A dictionary of JSON elements keyed by property name.</returns>
        public static IDictionary<string, JsonElement> ToJsonElements(this object value)
        {
            if (value == null)
            {
                return new Dictionary<string, JsonElement>();
            }

            if (value is Dictionary<string, JsonElement> result)
            {
                return result;
            }

            var elements = new Dictionary<string, JsonElement>();

            if (value is string json)
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var document = JsonDocument.Parse(json);
                    foreach (var property in document.RootElement.Clone().EnumerateObject())
                    {
                        elements.Add(property.Name, property.Value);
                    }
                }
            }
            else
            {
                var serialized = JsonSerializer.Serialize(value, SerializationOptions);
                using var document = JsonDocument.Parse(serialized);
                foreach (var property in document.RootElement.Clone().EnumerateObject())
                {
                    elements.Add(property.Name, property.Value);
                }
            }

            return elements;
        }

        public static void Add(this IDictionary<string, JsonElement> target, object value)
        {
            var elements = value.ToJsonElements();
            foreach (var prop in elements)
            {
                target.Add(prop.Key, prop.Value);
            }
        }

        /// <summary>
        /// Convert an object to the desired type using serialization and deserialization.
        /// </summary>
        /// <param name="value">The object to be converted to desired type: string, MemoryStream, object</param>
        /// <param name="defaultFactory"></param>
        /// <typeparam name="T">The type of object to convert to.</typeparam>
        /// <returns>The converted object.</returns>
        public static T ToObject<T>(object value, Func<T> defaultFactory = null)
        {
            if (value == null)
            {
                if (defaultFactory != null)
                {
                    return defaultFactory();
                }

                return default;
            }

            if (value is T result)
            {
                return result;
            }

            if (value is string json)
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    if (defaultFactory != null)
                    {
                        return defaultFactory();
                    }

                    return default;
                }

                return JsonSerializer.Deserialize<T>(json, SerializationOptions);
            }
            else if (value is Stream stream)
            {
                return JsonSerializer.Deserialize<T>(stream, SerializationOptions);
            }
            else if (value is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(jsonElement, SerializationOptions);
            }
            else if (value is JsonObject jsonObject)
            {
                return JsonSerializer.Deserialize<T>(jsonObject, SerializationOptions);
            }
            else if (value is JsonNode jsonNode)
            {
                return JsonSerializer.Deserialize<T>(jsonNode, SerializationOptions);
            }

            JsonElement serialized = JsonSerializer.SerializeToElement(value, value.GetType(), SerializationOptions);
            return JsonSerializer.Deserialize<T>(serialized, SerializationOptions);
        }

        public static bool Equals<T>(T value1, T value2)
        {
            return string.Equals(
                    JsonSerializer.Serialize(value1, SerializationOptions),
                    JsonSerializer.Serialize(value2, SerializationOptions)
                );
        }

        public static T CloneTo<T>(object obj)
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj, SerializationOptions), SerializationOptions);
        }

        public static string ToJson(object value)
        {
            return JsonSerializer.Serialize(value, SerializationOptions);
        }

        public static ToT GetAs<ToT, FromT>(FromT source)
        {
            return JsonSerializer.Deserialize<ToT>(JsonSerializer.Serialize(source, SerializationOptions), SerializationOptions);
        }
    }
}
