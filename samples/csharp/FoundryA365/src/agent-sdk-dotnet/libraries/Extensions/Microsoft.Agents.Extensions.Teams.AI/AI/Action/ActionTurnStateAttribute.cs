using Microsoft.Agents.Extensions.Teams.AI.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Action
{
    /// <summary>
    /// Attribute to represent the <see cref="Microsoft.Agents.Builder.State.TurnState"/> parameter of an action method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class ActionTurnStateAttribute : ActionParameterAttribute
    {
        /// <summary>
        /// Create new <see cref="Microsoft.Agents.Extensions.Teams.AI.Action.ActionTurnStateAttribute"/>.
        /// </summary>
        public ActionTurnStateAttribute() : base(ActionParameterType.TurnState) { }
    }
}
