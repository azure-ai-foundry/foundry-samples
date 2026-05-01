using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using Microsoft.Agents.Extensions.Teams.AI.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Action
{
    /// <summary>
    /// The data for default DO command action handler.
    /// </summary>
    /// <typeparam name="TState">Type of turn state.</typeparam>
    internal sealed class DoCommandActionData<TState> where TState : ITurnState
    {
        /// <summary>
        /// The predicted DO command.
        /// </summary>
        public PredictedDoCommand? PredictedDoCommand { get; set; }

        /// <summary>
        /// The handler to perform the predicted command.
        /// </summary>
        public IActionHandler<TState>? Handler { get; set; }
    }
}
