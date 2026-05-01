using Json.Schema;
using Microsoft.Agents.Extensions.Teams.AI.Utilities.JsonConverters;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Teams.AI.Planners
{
    /// <summary>
    /// A plan is a list of commands that the AI system should execute.
    /// </summary>
    [JsonConverter(typeof(PlanJsonConverter))]
    public class Plan
    {
        private static readonly string[] _defaultEnum = ["plan"];
        private static readonly string[] _defaultRequired = ["type", "commands"];

        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// A list of predicted commands that the AI system should execute.
        /// </summary>
        [JsonPropertyName("commands")]
        public List<IPredictedCommand> Commands { get; set; }

        /// <summary>
        /// Type to indicate that a plan is being returned.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; } = AIConstants.Plan;

        /// <summary>
        /// Creates a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.AI.Planners.Plan"/> class.
        /// </summary>
        public Plan()
        {
            Commands = new List<IPredictedCommand>();
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.AI.Planners.Plan"/> class.
        /// </summary>
        /// <param name="commands">A list of model predicted commands.</param>
        [JsonConstructor]
        public Plan(List<IPredictedCommand> commands)
        {
            Commands = commands;
        }

        /// <summary>
        /// Returns a Json string representation of the plan.
        /// </summary>
        public string ToJsonString()
        {
#pragma warning disable CA2263 // Prefer generic overload when type is known
            return JsonSerializer.Serialize(this, typeof(Plan), _serializerOptions);
#pragma warning restore CA2263 // Prefer generic overload when type is known
        }

        /// <summary>
        /// Returns a Json string representation of the plan.
        /// </summary>
        public override string ToString()
        {
            return this.ToJsonString();
        }

        /// <summary>
        /// Schema
        /// </summary>
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
                        "commands",
                        new JsonSchemaBuilder()
                            .Items(
                                new JsonSchemaBuilder()
                                    .OneOf(new JsonSchema[]
                                    {
                                        PredictedDoCommand.Schema(),
                                        PredictedSayCommand.Schema()
                                    })
                            )
                            .MinItems(1)
                    )
                )
                .Required(_defaultRequired)
                .Build();
        }
    }
}
