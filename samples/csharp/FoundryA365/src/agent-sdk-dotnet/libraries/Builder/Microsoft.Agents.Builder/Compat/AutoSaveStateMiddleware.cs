// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Compat
{
    /// <summary>
    ///  Middleware to automatically persist state before the end of each turn.
    /// </summary>
    /// <remarks>
    /// This calls <see cref="IAgentState.SaveChangesAsync(ITurnContext, bool, CancellationToken)"/>
    /// on each state object it manages.
    /// </remarks>
    public class AutoSaveStateMiddleware : IMiddleware
    {
        private readonly bool _autoLoad;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoSaveStateMiddleware"/> class.
        /// </summary>
        /// <param name="agentStates">initial list of <see cref="AgentState"/> objects to manage.</param>
        public AutoSaveStateMiddleware(params IAgentState[] agentStates)
        {
            // This is really so back-compat Agents can use the new AgentState methods without having to
            // Load or use IStatePropertyAccessor.
            _autoLoad = true;  
            TurnState = new TurnState(agentStates);
        }

        /// <summary>
        /// Allows for optionally auto-loading AgentState at turn start.
        /// </summary>
        /// <param name="autoLoad"></param>
        /// <param name="agentStates"></param>
        public AutoSaveStateMiddleware(bool autoLoad, params IAgentState[] agentStates)
        {
            _autoLoad = autoLoad;
            TurnState = new TurnState(agentStates);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoSaveStateMiddleware"/> class with 
        /// a list of state management objects managed by this object.
        /// </summary>
        /// <param name="turnState">The state management objects managed by this object.</param>
        public AutoSaveStateMiddleware(ITurnState turnState)
        {
            TurnState = turnState;
        }

        /// <summary>
        /// Gets or sets the list of state management objects managed by this object.
        /// </summary>
        /// <value>The state management objects managed by this object.</value>
        public ITurnState TurnState { get; set; }

        /// <summary>
        /// Before the turn ends, calls <see cref="AgentState.SaveChangesAsync(ITurnContext, bool, CancellationToken)"/>
        /// on each state object.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="next">The delegate to call to continue the Agent middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>This middleware persists state after the Agent logic completes and before the turn ends.</remarks>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            // before turn
            if (_autoLoad)
            {
                await TurnState.LoadStateAsync(turnContext, cancellationToken:cancellationToken, force:true).ConfigureAwait(false);
            }

            await next(cancellationToken).ConfigureAwait(false);

            // after turn
            await TurnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
