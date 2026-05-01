// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.SharePoint.Serialization.Converters;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.SharePoint.Serialization
{
    internal static class SerializerExtensions
    {
        public static JsonSerializerOptions ApplySharePointOptions(this JsonSerializerOptions options)
        {
            options.Converters.Add(new AceDataConverter());
            options.Converters.Add(new AceRequestConverter());

            return options;
        }
    }
}
