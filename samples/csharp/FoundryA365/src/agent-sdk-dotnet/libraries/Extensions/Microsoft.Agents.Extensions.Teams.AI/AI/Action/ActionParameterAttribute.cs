using System;

namespace Microsoft.Agents.Extensions.Teams.AI.Action
{
    /// <summary>
    /// Attribute that marks a method parameter as an action parameter.
    /// </summary>
#pragma warning disable CA1018 // Mark attributes with AttributeUsageAttribute
    public class ActionParameterAttribute : Attribute
#pragma warning restore CA1018 // Mark attributes with AttributeUsageAttribute
    {
        /// <summary>
        /// The attribute type that represents what the action parameter is.
        /// </summary>
        internal ActionParameterType ActionParameterType { get; set; }

        internal ActionParameterAttribute(ActionParameterType actionParameterType)
        {
            ActionParameterType = actionParameterType;
        }
    }

    /// <summary>
    /// Represent what the action parameter is.
    /// </summary>
    internal enum ActionParameterType
    {
        /// <summary>
        /// The action parameter is TurnContext
        /// </summary>
        TurnContext,
        /// <summary>
        /// The action parameter is TurnState
        /// </summary>
        TurnState,
        /// <summary>
        /// The action parameter is data object
        /// </summary>
        Parameters,
        /// <summary>
        /// The action parameter is action name
        /// </summary>
        Name,
        /// <summary>
        /// The action parameter is not attributed, use default value
        /// </summary>
        Unknown,
    }
}
