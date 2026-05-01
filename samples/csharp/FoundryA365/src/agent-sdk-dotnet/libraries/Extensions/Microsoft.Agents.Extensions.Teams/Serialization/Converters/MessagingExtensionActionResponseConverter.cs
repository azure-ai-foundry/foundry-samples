// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization.Converters;
using Microsoft.Agents.Extensions.Teams.Models;

namespace Microsoft.Agents.Extensions.Teams.Serialization.Converters
{
    // This is required because ConnectorConverter supports derived type handling.
    // In this case for the 'Task' property of type TaskModuleResponseBase.
    internal class MessagingExtensionActionResponseConverter : ConnectorConverter<MessagingExtensionActionResponse>
    {
    }
}
