// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Conversation accounts represent the identity of conversations within a channel. In channels that support only a
    /// single conversation between two accounts (e.g. SMS), the conversation account is persistent and does not have a 
    /// predetermined start or end. In channels that support multiple parallel conversations (e.g. email), each 
    /// conversation will likely have a unique ID.
    /// </summary>
    public class ConversationAccount
    {
        /// <summary> Initializes a new instance of ConversationAccount. </summary>
        public ConversationAccount()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Microsoft.Agents.Core.Models.ConversationAccount"/> class.</summary>
        /// <param name="isGroup">Indicates whether the conversation contains more than two participants at the time the activity was generated.</param>
        /// <param name="conversationType">Indicates the type of the conversation in channels that distinguish between conversation types.</param>
        /// <param name="id">Channel id for the user or Agent on this channel (Example: joe@smith.com, or @joesmith or 123456).</param>
        /// <param name="name">Display friendly name.</param>
        /// <param name="aadObjectId">This account's object ID within Azure Active Directory (AAD).</param>
        /// <param name="role">Role of the entity behind the account (Example: User, Agent, etc.). Possible values include: 'user', 'agent'.</param>
        /// <param name="tenantId">This conversation's tenant ID.</param>
        public ConversationAccount(bool? isGroup = default, string conversationType = default, string id = default, string name = default, string aadObjectId = default, string role = default, string tenantId = default)
        {
            IsGroup = isGroup;
            ConversationType = conversationType;
            Id = id;
            Name = name;
            AadObjectId = aadObjectId;
            Role = role;
            TenantId = tenantId;
        }

        /// <summary> Indicates whether the conversation contains more than two participants at the time the activity was generated. </summary>
        public bool? IsGroup { get; set; }
        /// <summary> Indicates the type of the conversation in channels that distinguish between conversation types. See <see cref="Microsoft.Agents.Core.Models.ConversationTypes"/></summary>
        public string ConversationType { get; set; }
        /// <summary> This conversation's tenant ID. </summary>
        public string TenantId { get; set; }
        /// <summary> Channel id for the user or Agent on this channel. The format of this ID is defined by the channel and is used as an opaque string throughout the protocol. (Example: joe@smith.com, or @joesmith or 123456). </summary>
        public string Id { get; set; }
        /// <summary> Display friendly name. </summary>
        public string Name { get; set; }
        /// <summary> This account's object ID within Azure Active Directory (AAD). </summary>
        public string AadObjectId { get; set; }
        /// <summary> Role of the entity behind the account (Example: User, Agent, etc.). See RoleTypes.</summary>
        public string Role { get; set; }

        /// <summary>
        /// Gets properties that are not otherwise defined by the <see cref="Microsoft.Agents.Core.Models.Activity"/> type but that
        /// might appear in the serialized REST JSON object.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        [JsonExtensionData]
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
