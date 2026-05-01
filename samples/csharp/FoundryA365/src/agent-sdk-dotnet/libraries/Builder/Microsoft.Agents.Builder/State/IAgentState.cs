// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.State
{
    /// <summary>
    /// Defines a state management object and automates the reading and writing of associated state
    /// properties to a storage layer.
    /// </summary>
    /// <remarks>
    /// Each state management object defines a scope for a storage layer.
    ///
    /// State properties are created within a state management scope, and the Agents SDK
    /// defines these scopes:
    /// <see cref="ConversationState"/>, <see cref="UserState"/>, and <see cref="PrivateConversationState"/>.
    ///
    /// You can define additional scopes for your Agent.
    /// </remarks>
    /// <seealso cref="Microsoft.Agents.Storage.IStorage"/>
    public interface IAgentState
    {
        /// <summary>
        /// The scope name of the state.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Get a property value.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="defaultValueFactory">Defines the default value.
        /// Invoked when no value been set for the requested state property.
        /// If defaultValueFactory is defined as null in that case, the method returns null.</param>
        T GetValue<T>(string name, Func<T> defaultValueFactory = null);

        /// <summary>
        /// Set a property value.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The property value.</param>
        void SetValue<T>(string name, T value);

        /// <summary>
        /// Checks for the existence of a property.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        bool HasValue(string name);

        /// <summary>
        /// Delete a property.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        void DeleteValue(string name);

        /// <summary>
        /// True if state has been loaded.
        /// </summary>
        bool IsLoaded();

        /// <summary>
        /// Clears the state.
        /// </summary>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>This method clears the state cache. Call
        /// <see cref="SaveChangesAsync(ITurnContext, bool, CancellationToken)"/> to persist this
        /// change in the storage layer.
        /// </remarks>
        void ClearState();

        /// <summary>
        /// Populates state from the storage layer.
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
        Task LoadAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes state to the storage layer.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="force">Optional, <c>true</c> to save the state cache to storage;
        /// or <c>false</c> to save state to storage only if a property in the cache has changed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="turnContext"/> is <c>null</c>.</exception>
        Task SaveChangesAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes state in storage.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="turnContext"/> is <c>null</c>.</exception>
        Task DeleteStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default);
    }
}