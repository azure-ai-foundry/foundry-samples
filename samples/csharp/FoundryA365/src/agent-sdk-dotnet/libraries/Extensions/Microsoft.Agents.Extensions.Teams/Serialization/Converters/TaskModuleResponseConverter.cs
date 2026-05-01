// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization.Converters;
using Microsoft.Agents.Extensions.Teams.Models;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Teams.Serialization.Converters
{
    // This is required because ConnectorConverter supports derived type handling.
    // In this case for the 'Task' property of type TaskModuleResponse.
    internal class TaskModuleResponseConverter : ConnectorConverter<TaskModuleResponse>
    {
        protected override bool TryReadCollectionProperty(ref Utf8JsonReader reader, TaskModuleResponse value, string propertyName, JsonSerializerOptions options)
        {
            PropertyInfo propertyInfo = typeof(TaskModuleResponse).GetProperty(propertyName);
            if (propertyInfo != null && propertyInfo.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType))
            {
                return true;
            }
            return false;
        }

        protected override bool TryReadGenericProperty(ref Utf8JsonReader reader, TaskModuleResponse value, string propertyName, JsonSerializerOptions options)
        {
            return false;
        }

        protected override void ReadExtensionData(ref Utf8JsonReader reader, TaskModuleResponse value, string propertyName, JsonSerializerOptions options)
        {
        }

        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, TaskModuleResponse value, string propertyName, JsonSerializerOptions options)
        {
            return false;
        }

        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, TaskModuleResponse value, string propertyName)
        {
            return false;
        }
    }
}
