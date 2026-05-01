// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Client;

namespace Microsoft.Agents.Builder.Dialogs
{
    /// <summary>
    /// Defines the options that will be used to execute a <see cref="SkillDialog"/>.
    /// </summary>
    public class SkillDialogOptions
    {
        [JsonIgnore]
        public IAgentHost AgentHost { get; set; }

        /// <summary>
        /// Gets or sets the Channel name the dialog will call.
        /// </summary>
        /// <value>
        /// The Agent name that the Dialog will call.
        /// </value>
        [JsonPropertyName("skill")]
        public string Skill { get; set; }


        /// <summary>
        /// Gets or sets the Microsoft app ID of the Agent calling the skill.
        /// </summary>
        /// <value>
        /// The the Microsoft app ID of the Agent calling the skill.
        /// </value>
        [JsonIgnore]
        public string ClientId => AgentHost?.HostClientId;

        /// <summary>
        /// Gets or sets the <see cref="IAgentClient"/> used to call the remote Agent.
        /// </summary>
        /// <value>
        /// The <see cref="IAgentClient"/> used to call the remote Agent.
        /// </value>
        [JsonIgnore]
        public IAgentClient SkillClient => AgentHost?.GetClient(Skill);

        /// <summary>
        /// Gets or sets the <see cref="ConversationState"/> to be used by the dialog.
        /// </summary>
        /// <value>
        /// The <see cref="ConversationState"/> to be used by the dialog.
        /// </value>
        [JsonIgnore]
        public ConversationState ConversationState { get; set; }

        /// <summary>
        /// Gets or sets the OAuth Connection Name, that would be used to perform Single SignOn with a skill.
        /// </summary>
        /// <value>
        /// The Token Provider Connection Name to use to get tokens.
        /// </value>
        [JsonPropertyName("connectionName")]
        public string ConnectionName { get; set; }
    }
}
