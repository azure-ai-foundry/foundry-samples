// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Serialization.Converters;
using Microsoft.Agents.Extensions.Teams.Models;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Teams.Serialization.Converters
{
    // This is required because ConnectorConverter supports derived type handling.
    // In this case for the 'Task' property of type TaskModuleResponseBase.
    internal class MessagingExtensionAttachmentConverter : ConnectorConverter<MessagingExtensionAttachment>
    {
        /// <inheritdoc/>
        protected override void ReadExtensionData(ref Utf8JsonReader reader, MessagingExtensionAttachment value, string propertyName, JsonSerializerOptions options)
        {
            var extensionData = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
            value.Properties.Add(propertyName, extensionData);
        }

        /// <inheritdoc/>
        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, MessagingExtensionAttachment value, string propertyName, JsonSerializerOptions options)
        {
            if (!propertyName.Equals(nameof(value.Properties)))
            {
                return false;
            }

            var propertyValue = JsonSerializer.Deserialize<object>(ref reader, options);

            foreach (var element in propertyValue.ToJsonElements())
            {
                value.Properties.Add(element.Key, element.Value);
            }

            return true;
        }

        /// <inheritdoc/>
        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, MessagingExtensionAttachment value, string propertyName)
        {
            if (!propertyName.Equals(nameof(value.Properties)))
            {
                return false;
            }

            foreach (var extensionData in value.Properties)
            {
                writer.WritePropertyName(extensionData.Key);
                extensionData.Value.WriteTo(writer);
            }

            return true;
        }
    }
}
