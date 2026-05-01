using Microsoft.Agents.Extensions.Teams.AI.Moderator;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.AI.Utilities;

namespace Microsoft.Agents.Extensions.Teams.AI
{
    /// <summary>
    /// Options for configuring the AI system.
    /// </summary>
    /// <typeparam name="TState">The turn state class.</typeparam>
    public sealed class AIOptions
    {
        /// <summary>
        /// The planner to use for generating plans.
        /// </summary>
        public IPlanner<ITurnState> Planner { get; set; }

        /// <summary>
        /// Optional. The moderator to use for moderating input passed to the model and the output
        /// returned by the model.
        /// </summary>
        /// <remarks>
        /// The default value is an instance of <see cref="Microsoft.Agents.Extensions.Teams.AI.Moderator.DefaultModerator{TState}"/>
        /// </remarks>
        public IModerator<ITurnState>? Moderator { get; set; }

        /// <summary>
        /// Optional. Maximum number of actions to execute in a single turn.
        /// </summary>
        /// <remarks>
        /// The default value is 25.
        /// </remarks>
        public int? MaxSteps { get; set; }

        /// <summary>
        /// Optional. Maximum amount of time to spend executing a single turn in milliseconds.
        /// </summary>
        /// <remarks>
        /// The default value is 300000 or 5 minutes.
        /// </remarks>
        public TimeSpan? MaxTime { get; set; }

        /// <summary>
        /// Optional. If true, the AI system will allow the planner to loop.
        /// </summary>
        /// <remarks>
        /// The default value is `true`.
        /// 
        /// Looping is needed for augmentations like `functions` and `monologue` where the LLM needs to
        /// see the result of the last action that was performed. The AI system will attempt to autodetect
        /// if it needs to loop so you generally don't need to worry about this setting.
        ///
        /// If you're using an augmentation like `sequence` you can set this to `false` to guard against
        /// any accidental looping.
        /// </remarks>
        public bool? AllowLooping { get; set; }

        /// <summary>
        /// Optional. If true, the AI system will enable the feedback loop in Teams that allows a user to give thumbs up or down to a response.
        /// Defaults to "false".
        /// </summary>
        public bool EnableFeedbackLoop { get; set; } = false;

        /// <summary>
        /// Represents the type of feedback loop. Set to "default" by default. It can be set to one of "default" or "custom".
        /// The <see cref="Microsoft.Agents.Extensions.Teams.AI.AIOptions{TState}.EnableFeedbackLoop"/> property should be set to true to use this property.
        /// </summary>
        public string FeedbackLoopType { get; set; } = "default";

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.AI.AIOptions{TState}"/> class.
        /// </summary>
        /// <param name="planner">The planner to use for generating plans.</param>
        public AIOptions(IPlanner<ITurnState> planner)
        {
            Verify.ParamNotNull(planner);

            Planner = planner;
        }
    }
}
