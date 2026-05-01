using System;

namespace Microsoft.Agents.Extensions.Teams.AI.Action
{
    /// <summary>
    /// Attribute to represent the action parameters of an action method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class ActionParametersAttribute : ActionParameterAttribute
    {
        /// <summary>
        /// Create new <see cref="Microsoft.Agents.Extensions.Teams.AI.Action.ActionParametersAttribute"/>.
        /// </summary>
        public ActionParametersAttribute() : base(ActionParameterType.Parameters) { }
    }
}
