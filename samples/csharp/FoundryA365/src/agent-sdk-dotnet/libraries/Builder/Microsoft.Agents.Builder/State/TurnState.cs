// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Agents.Core;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.State
{
    /// <summary>
    ///  Manages a collection of AgentState and provides ability to load and save in parallel.
    /// </summary>
    public class TurnState : ITurnState
    {
        private readonly Dictionary<string, IAgentState> _scopes = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="TurnState"/> class.
        /// </summary>
        /// <param name="agentStates">initial list of <see cref="AgentState"/> objects to manage.</param>
        public TurnState(params IAgentState[] agentStates)
        {
            foreach (var agentState in agentStates)
            {
                _scopes.Add(agentState.Name, agentState);
            }

            if (!TryGetScope<TempState>(out _))
            {
                _scopes.Add(TempState.ScopeName, new TempState());
            }
        }

        /// <summary>
        /// Creates AgentStateSet with default ConversationState and UserState
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="agentStates">Additional list of AgentState objects to manage.</param>
        public TurnState(IStorage storage, params IAgentState[] agentStates)
        {
            _scopes.Add(ConversationState.ScopeName, new ConversationState(storage));
            _scopes.Add(UserState.ScopeName, new UserState(storage));
            _scopes.Add(TempState.ScopeName, new TempState());

            foreach (var agentState in agentStates)
            {
                _scopes[agentState.Name] = agentState;
            }
        }

        public ConversationState Conversation => GetScope<ConversationState>();
        public UserState User => GetScope<UserState>();
        public PrivateConversationState Private => GetScope<PrivateConversationState>();
        public TempState Temp => GetScope<TempState>();

        public virtual bool HasValue(string path)
        {
            var (scope, property) = GetScopeAndPath(path);
            return GetScope(scope).HasValue(property);
        }

        public T GetValue<T>(string name, Func<T> defaultValueFactory = null)
        {
            var (scope, property) = GetScopeAndPath(name);
            return GetScope(scope).GetValue(property, defaultValueFactory);
        }

        public virtual void SetValue(string path, object value)
        {
            var (scope, property) = GetScopeAndPath(path);
            GetScope(scope).SetValue(property, value);
        }

        public virtual void DeleteValue(string path)
        {
            var (scope, property) = GetScopeAndPath(path);
            GetScope(scope).DeleteValue(property);
        }

        public IAgentState GetScope(string scope)
        {
            if (!_scopes.TryGetValue(scope, out IAgentState value))
            {
                throw new ArgumentException($"Scope '{scope}' not found");
            }
            return value;
        }

        public T GetScope<T>()
        {
            if (TryGetScope<T>(out var scope))
            {
                return scope;
            }
            throw new ArgumentException($"Scope '{nameof(T)}' not found");
        }

        public bool TryGetScope<T>(out T value)
        {
            foreach (var scope in _scopes)
            {
                if (scope.Value is T agentState)
                {
                    value = agentState;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public TurnState Add(IAgentState agentState)
        {
            AssertionHelpers.ThrowIfNull(agentState, nameof(agentState));
            _scopes.Add(agentState.Name, agentState);
            return this;
        }

        /// <summary>
        /// Load all AgentState records in parallel.
        /// </summary>
        /// <param name="turnContext">turn context.</param>
        /// <param name="force">should data be forced into cache.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task LoadStateAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            var tasks = _scopes.Select(bs => bs.Value.LoadAsync(turnContext, force, cancellationToken)).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public void ClearState(string scope)
        {
            GetScope(scope).ClearState();
        }

        /// <summary>
        /// Save All AgentState changes in parallel.
        /// </summary>
        /// <param name="turnContext">turn context.</param>
        /// <param name="force">should data be forced to save even if no change were detected.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task SaveStateAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            var tasks = _scopes.Select(kv => kv.Value.SaveChangesAsync(turnContext, force, cancellationToken)).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static (string, string) GetScopeAndPath(string name)
        {
            var scopeEnd = name.IndexOf('.');
            if (scopeEnd == -1)
            {
                return (TempState.ScopeName, name);
            }
            return (name[..scopeEnd], name[(scopeEnd + 1)..]);
        }
    }
}
