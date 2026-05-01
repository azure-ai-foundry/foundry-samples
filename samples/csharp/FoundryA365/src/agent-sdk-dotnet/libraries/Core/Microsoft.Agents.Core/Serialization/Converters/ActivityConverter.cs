// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class ActivityConverter : ConnectorConverter<Activity>
    {
        public override Activity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var activity = base.Read(ref reader, typeToConvert, options);

            var productInfo = activity.GetProductInfoEntity();
            if (productInfo != null)
            {
                if (activity.ChannelId != null)
                {
                    activity.ChannelId.SubChannel = productInfo.Id;
                }
            }

            return activity;
        }

        public override void Write(Utf8JsonWriter writer, Activity value, JsonSerializerOptions options)
        {
            var productInfo = value.GetProductInfoEntity();
            if (value.ChannelId != null && value.ChannelId.IsSubChannel())
            {
                if (productInfo != null)
                {
                    productInfo.Id = value.ChannelId.SubChannel;
                }
                else
                {
                    value.Entities.Add(new ProductInfo() { Id = value.ChannelId.SubChannel });
                }
            }
            else if (productInfo != null)
            {
                value.Entities.Remove(productInfo);
            }

            base.Write(writer, value, options);
        }

        /// <inheritdoc/>
        protected override bool TryReadCollectionProperty(ref Utf8JsonReader reader, Activity value, string propertyName, JsonSerializerOptions options)
        {
            PropertyInfo propertyInfo = typeof(Activity).GetProperty(propertyName);
            if (propertyInfo != null  && propertyInfo.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType))
            {
                return true;
            }
            return false;
        }

        protected override void ReadProperty(ref Utf8JsonReader reader, Activity value, string propertyName, JsonSerializerOptions options, PropertyInfo property)
        {
            if (propertyName.Equals("channelId", System.StringComparison.OrdinalIgnoreCase))
            {
                var propertyValue = JsonSerializer.Deserialize<string>(ref reader, options);
                property.SetValue(value, new ChannelId(propertyValue, ProtocolJsonSerializer.ChannelIdIncludesProduct));
                return;
            }

            base.ReadProperty(ref reader, value, propertyName, options, property);
        }

        /// <inheritdoc/>
        protected override bool TryReadGenericProperty(ref Utf8JsonReader reader, Activity value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.ChannelData)))
            {
                SetGenericProperty(ref reader, data => value.ChannelData = data, options);
            }
            else if (propertyName.Equals(nameof(value.Value)))
            {
                SetGenericProperty(ref reader, data => value.Value = data, options);
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        protected override void ReadExtensionData(ref Utf8JsonReader reader, Activity value, string propertyName, JsonSerializerOptions options)
        {
            var extensionData = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
            value.Properties.Add(propertyName, extensionData);
        }

        /// <inheritdoc/>
        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, Activity value, string propertyName, JsonSerializerOptions options)
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
        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, Activity value, string propertyName)
        {
            if (propertyName.Equals(nameof(value.ChannelId)))
            {
                if (value.ChannelId != null)
                {
                    writer.WriteString("channelId", value.ChannelId.Channel);
                }
                return true;
            }

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
