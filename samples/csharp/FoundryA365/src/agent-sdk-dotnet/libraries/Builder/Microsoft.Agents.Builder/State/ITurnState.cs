// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.State
{
    /// <summary>
    /// TurnState represents the state for an Agent.  State is composed of 1+ state scopes.
    /// </summary>
    public interface ITurnState
    {
        ConversationState Conversation { get; }
        PrivateConversationState Private { get; }
        TempState Temp { get; }
        UserState User { get; }

        IAgentState GetScope(string scope);
        T GetScope<T>();

        /// <summary>
        /// Get a property value.
        /// </summary>
        /// <param name="path">The full path to the property: `{scope}.{name}`.  If `{scope}` is missing the value is a get on <see cref="Temp"/>.</param>
        /// <param name="defaultValueFactory">Defines the default value.
        /// Invoked when no value been set for the requested state property.
        /// If defaultValueFactory is defined as null in that case, the method returns null.</param>
        /// <remarks>
        /// `{scope}` is always the name of an <see cref="IAgentState.Name"/>.
        /// </remarks>
        T GetValue<T>(string path, Func<T> defaultValueFactory = null);

        /// <summary>
        /// Set a property value.
        /// </summary>
        /// <param name="path">The full path to the property: `{scope}.{name}`.  If `{scope}` is missing the value is a set on <see cref="Temp"/>.</param>
        /// <param name="value">The property value.</param>
        /// <remarks>
        /// `{scope}` is always the name of an <see cref="IAgentState.Name"/>.
        /// </remarks>
        void SetValue(string path, object value);

        /// <summary>
        /// Delete a property.
        /// </summary>
        /// <param name="path">The full path to the property: `{scope}.{name}`. If `{scope}` is missing the value is deleted on <see cref="Temp"/>.</param>
        /// <remarks>
        /// `{scope}` is always the name of an <see cref="IAgentState.Name"/>.
        /// </remarks>
        void DeleteValue(string path);

        /// <summary>
        /// Checks for the existence of a property.
        /// </summary>
        /// <param name="path">The full path to the property: `{scope}.{name}`. If `{scope}` is missing the value is against <see cref="Temp"/>.</param>
        /// <remarks>
        /// `{scope}` is always the name of an <see cref="IAgentState.Name"/>.
        /// </remarks>
        bool HasValue(string path);

        /// <summary>
        /// Clears the state.
        /// </summary>
        /// <param name="scope">The scope name.  eg "conversation", etc...</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>
        /// `scope` is always the name of an <see cref="IAgentState.Name"/>.
        /// </remarks>
        /// <remarks>This method clears the state cache. Call
        /// <see cref="SaveChangesAsync(ITurnContext, bool, CancellationToken)"/> to persist this
        /// change in the storage layer.
        /// </remarks>
        void ClearState(string scope);

        /// <summary>
        /// Populates all states from the storage layer.
        /// </summary>
        /// <remarks>
        /// LoadAsync loads State for the specified turn.
        /// </remarks>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="force">Optional, <c>true</c> to overwrite any existing state cache;
        /// or <c>false</c> to load state from storage only if the cache doesn't already exist.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="turnContext"/> is <c>null</c>.</exception>
        Task LoadStateAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes all states to the storage layer.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="force">Optional, <c>true</c> to save the state cache to storage;
        /// or <c>false</c> to save state to storage only if a property in the cache has changed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="turnContext"/> is <c>null</c>.</exception>
        Task SaveStateAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default);
    }
}