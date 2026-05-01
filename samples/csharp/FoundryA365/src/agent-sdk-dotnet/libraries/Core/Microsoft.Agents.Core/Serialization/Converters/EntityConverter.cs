// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Linq;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class EntityConverter : ConnectorConverter<Entity>
    {
        public override Entity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var localReader = reader;
            using var doc = JsonDocument.ParseValue(ref localReader);

            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProperty) || typeProperty.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Type discriminator not found.");
            }

            var typeDiscriminator = typeProperty.GetString();
            if (string.IsNullOrEmpty(typeDiscriminator))
            {
                throw new JsonException("Type discriminator not found.");
            }

            var toType = ProtocolJsonSerializer.EntityTypes.Where(w => w.Key.Equals(typeDiscriminator, StringComparison.OrdinalIgnoreCase)).FirstOrDefault().Value ?? typeToConvert;
            return base.Read(ref reader, toType, options);
        }

        protected override void ReadExtensionData(ref Utf8JsonReader reader, Entity value, string propertyName, JsonSerializerOptions options)
        {
            var extensionData = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
            value.Properties.Add(propertyName, extensionData);
        }

        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, Entity value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.Properties)))
            {
                var propertyValue = JsonSerializer.Deserialize<object>(ref reader, options);

                foreach (var element in propertyValue.ToJsonElements())
                {
                    value.Properties.Add(element.Key, element.Value);
                }

                return true;
            }

            return false;
        }

        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, Entity value, string propertyName)
        {
            if (propertyName.Equals(nameof(value.Properties)))
            {
                foreach (var extensionData in value.Properties)
                {
                    writer.WritePropertyName(extensionData.Key);
                    extensionData.Value.WriteTo(writer);
                }

                return true;
            }

            return false;
        }
    }
}
