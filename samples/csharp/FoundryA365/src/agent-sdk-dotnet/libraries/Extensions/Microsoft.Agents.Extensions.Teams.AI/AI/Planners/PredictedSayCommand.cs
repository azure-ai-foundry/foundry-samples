using Json.Schema;
using System.Text.Json.Serialization;
using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Extensions.Teams.AI.Utilities.JsonConverters;

namespace Microsoft.Agents.Extensions.Teams.AI.Planners
{
    /// <summary>
    /// A predicted SAY command is a response that the AI system should say.
    /// </summary>
    public class PredictedSayCommand : IPredictedCommand
    {
        private static readonly string[] _defaultEnum = ["SAY"];
        private static readonly string[] _defaultRequired = ["type", "response"];

        /// <summary>
        /// The type to indicate that a SAY command is being returned.
        /// </summary>
        public string Type { get; } = AIConstants.SayCommand;

        /// <summary>
        /// The response that the AI system should say.
        /// </summary>
        [JsonPropertyName("response")]
        [JsonConverter(typeof(ChatMessageJsonConverter))]
        [JsonRequired]
        public ChatMessage Response { get; set; }

        /// <summary>
        /// Creates a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.AI.Planners.PredictedSayCommand"/> class.
        /// </summary>
        /// <param name="response">The response that the AI system should say.</param>
        [JsonConstructor]
        public PredictedSayCommand(ChatMessage response)
        {
            Response = response;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.AI.Planners.PredictedSayCommand"/> class.
        /// </summary>
        /// <param name="response">The response that the AI system should say.</param>
        public PredictedSayCommand(string response)
        {
            Response = new ChatMessage(ChatRole.Assistant)
            {
                Content = response
            };
        }

        /// <summary>
        /// Schema
        /// </summary>
        /// <returns></returns>
        public static JsonSchema Schema()
        {
            return new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    (
                        "type",
                        new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum(_defaultEnum)
                    ),
                    (
                        "response",
                        new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                    )
                )
                .Required(_defaultRequired)
                .Build();
        }
    }
}
