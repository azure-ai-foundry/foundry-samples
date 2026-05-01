// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Microsoft.Agents.Extensions.Teams.Serialization.Converters;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Teams.Serialization
{
    [SerializationInit]
    internal class SerializationInit
    {
        public static void Init()
        {
            var converters = new List<JsonConverter>
            {
                new SurfaceConverter(),
                new TabSubmitDataConverter(),
                new TeamsChannelDataConverter(),
                new MessagingExtensionActionResponseConverter(),
                new TaskModuleResponseConverter(),
                new TaskModuleResponseBaseConverter(),
                new TaskModuleCardResponseConverter(),
                new TaskModuleContinueResponseConverter(),
                new TaskModuleMessageResponseConverter(),
                new MessagingExtensionAttachmentConverter(),
                new TeamsChannelDataSettingsConverter(),
                new JsonStringEnumConverter<SurfaceType>(JsonNamingPolicy.CamelCase),
                new JsonStringEnumConverter<ContentType>(JsonNamingPolicy.CamelCase),
                new JsonStringEnumConverter<MembershipSourceTypes>(JsonNamingPolicy.CamelCase),
                new JsonStringEnumConverter<MembershipTypes>(JsonNamingPolicy.CamelCase)
            };

            ProtocolJsonSerializer.ApplyExtensionConverters(converters);
        }
    }
}
