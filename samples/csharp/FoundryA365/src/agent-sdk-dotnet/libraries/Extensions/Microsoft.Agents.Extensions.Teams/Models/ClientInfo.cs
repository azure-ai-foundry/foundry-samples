// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    [EntityName(EntityName)]
    public class ClientInfo : Entity
    {
        public const string EntityName = "clientInfo";

        public ClientInfo() : base(EntityName)
        {
        }

        public string? Locale { get; set; }

        public string? Country { get; set; }

        public string? Platform { get; set; }

        public string? Timezone { get; set; }
    }
}
