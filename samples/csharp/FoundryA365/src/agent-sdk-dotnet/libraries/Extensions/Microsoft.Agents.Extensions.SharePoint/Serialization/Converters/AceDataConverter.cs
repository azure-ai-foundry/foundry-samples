// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization.Converters;
using Microsoft.Agents.Extensions.SharePoint.Models;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.SharePoint.Serialization.Converters
{
    internal class AceDataConverter : ConnectorConverter<AceData>
    {
        protected override bool TryReadGenericProperty(ref Utf8JsonReader reader, AceData value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.Properties)))
            {
                SetGenericProperty(ref reader, data => value.Properties = data, options);
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
