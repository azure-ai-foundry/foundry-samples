using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.AI.State;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.Agents.Extensions.Teams.AI.Action
{
    internal class ActionCollection<TState> : IActionCollection<TState> where TState : ITurnState
    {
        private readonly Dictionary<string, ActionEntry<TState>> _actions;

        public ActionCollection()
        {
            _actions = new Dictionary<string, ActionEntry<TState>>();
        }

        /// <inheritdoc />
        public ActionEntry<TState> this[string actionName]
        {
            get
            {
                if (!_actions.TryGetValue(actionName, out ActionEntry<TState>? actionValue))
                {
                    throw new ArgumentException($"`{actionName}` action does not exist");
                }
                return actionValue;
            }
        }

        /// <inheritdoc />
        public void AddAction(string actionName, IActionHandler<TState> handler, bool allowOverrides = false)
        {
            if (_actions.ContainsKey(actionName))
            {
                if (!_actions[actionName].AllowOverrides)
                {
                    throw new ArgumentException($"Action {actionName} already exists and does not allow overrides");
                }
            }
            _actions[actionName] = new ActionEntry<TState>(actionName, handler, allowOverrides);
        }

        /// <inheritdoc />
        public bool ContainsAction(string actionName)
        {
            return _actions.ContainsKey(actionName);
        }

        public bool TryGetAction(string actionName, out ActionEntry<TState> actionEntry)
        {
            return _actions.TryGetValue(actionName, out actionEntry!);
        }
    }
}
