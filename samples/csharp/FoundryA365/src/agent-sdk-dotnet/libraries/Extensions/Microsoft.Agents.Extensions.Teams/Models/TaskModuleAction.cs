// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Core.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Adapter class to represent BotBuilder card action as adaptive card action (in type of Action.Submit).
    /// </summary>
    public class TaskModuleAction : CardAction
    {
        JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleAction"/> class.
        /// </summary>
        /// <param name="title">Button title.</param>
        /// <param name="value">Free hidden value binding with button. The value will be sent out with "task/fetch" invoke event.</param>
        public TaskModuleAction(string title, object value = null)
            : base("invoke", title)
        {
            JsonNode data;
            if (value == null)
            {
                data = JsonNode.Parse("{}");
            }
            else
            {
                if (value is string)
                {
                    data = JsonNode.Parse(value as string);
                }
                else if (value is JsonElement)
                {
                    data = JsonNode.Parse(((JsonElement) value).ToString());
                }
                else
                {
                    data = JsonNode.Parse(JsonSerializer.Serialize(value, jsonSerializerOptions));
                }
            }

            data["type"] = "task/fetch";
            this.Value = data.ToString();
        }
    }
}
