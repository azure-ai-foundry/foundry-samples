using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Schema;
using Microsoft.Agents.Extensions.Teams.AI.Utilities.JsonConverters;

namespace Microsoft.Agents.Extensions.Teams.AI.Planners
{
    /// <summary>
    /// A predicted DO command is an action that the AI system should perform.
    /// </summary>
    public class PredictedDoCommand : IPredictedCommand
    {
        private readonly static string[] _defaultEnum = ["DO"];
        private readonly static string[] _defaultRequired = ["type", "action"];

        /// <summary>
        /// Type to indicate that a DO command is being returned.
        /// </summary>
        public string Type { get; } = AIConstants.DoCommand;

        /// <summary>
        /// The named action that the AI system should perform.
        /// </summary>
        [JsonPropertyName("action")]
        public string Action { get; set; }

        /// <summary>
        /// Any parameters that the AI system should use to perform the action.
        /// </summary>
        [JsonPropertyName("parameters")]
        [JsonConverter(typeof(DictionaryJsonConverter))]
        public Dictionary<string, object?>? Parameters { get; set; }

        /// <summary>
        /// The id mapped to the named action that the AI system should perform.
        /// </summary>
        [JsonPropertyName("action_id")]
        public string? ActionId { get; set; }

        private static readonly JsonSerializerOptions _serializerOptions = _CreateJsonSerializerOptions();

        /// <summary>
        /// Creates a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.AI.Planners.PredictedDoCommand"/> class.
        /// </summary>
        /// <param name="action">The action name.</param>
        /// <param name="parameters">Any parameters that the AI system should use to perform the action.</param>
        public PredictedDoCommand(string action, Dictionary<string, object?> parameters)
        {
            Action = action;
            Parameters = parameters;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.AI.Planners.PredictedDoCommand"/> class where parameters is a valid JSON string.
        /// </summary>
        /// <param name="action">The action name.</param>
        /// <param name="parameters">Any parameters that the AI system should use to perform the action as a JSON string.</param>
        public PredictedDoCommand(string action, string parameters)
        {
            Action = action;
            Parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(parameters, _serializerOptions)!;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.AI.Planners.PredictedDoCommand"/> class.
        /// </summary>
        /// <param name="action"></param>
        [JsonConstructor]
        public PredictedDoCommand(string action)
        {
            Action = action;
            Parameters = new Dictionary<string, object?>();
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
                        "action",
                        new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                    ),
                    (
                        "parameters",
                        new JsonSchemaBuilder()
                            .Type(SchemaValueType.Object)
                    )
                )
                .Required(_defaultRequired)
                .Build();
        }

        private static JsonSerializerOptions _CreateJsonSerializerOptions()
        {
            JsonSerializerOptions options = new();
            options.Converters.Add(new DictionaryJsonConverter());
            return options;
        }
    }
}
