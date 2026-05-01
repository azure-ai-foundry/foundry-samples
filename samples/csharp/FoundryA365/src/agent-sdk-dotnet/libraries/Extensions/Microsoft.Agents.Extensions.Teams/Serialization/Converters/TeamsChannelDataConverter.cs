// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Serialization.Converters;
using Microsoft.Agents.Extensions.Teams.Models;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Teams.Serialization.Converters
{
    internal class TeamsChannelDataConverter : ConnectorConverter<TeamsChannelData>
    {
        protected override bool TryReadCollectionProperty(ref Utf8JsonReader reader, TeamsChannelData value, string propertyName, JsonSerializerOptions options)
        {
            PropertyInfo propertyInfo = typeof(TeamsChannelData).GetProperty(propertyName);
            if (propertyInfo != null && propertyInfo.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType))
            {
                return true;
            }
            return false;
        }

        protected override bool TryReadGenericProperty(ref Utf8JsonReader reader, TeamsChannelData value, string propertyName, JsonSerializerOptions options)
        {
            return false;
        }

        protected override void ReadExtensionData(ref Utf8JsonReader reader, TeamsChannelData value, string propertyName, JsonSerializerOptions options)
        {
            var extensionData = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(ref reader, options);
            value.Properties.Add(propertyName, extensionData);
        }

        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, TeamsChannelData value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.Properties)))
            {
                var propertyValue = System.Text.Json.JsonSerializer.Deserialize<object>(ref reader, options);

                foreach (var element in propertyValue.ToJsonElements())
                {
                    value.Properties.Add(element.Key, element.Value);
                }

                return true;
            }

            return false;
        }

        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, TeamsChannelData value, string propertyName)
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
