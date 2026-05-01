// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections.Concurrent;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Values persisted for the lifetime of the turn as part of the <see cref="ITurnContext"/>.
    /// </summary>
    public class TurnContextStateCollection : ConcurrentDictionary<string, object>, IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TurnContextStateCollection"/> class.
        /// </summary>
        public TurnContextStateCollection()
            : base(StringComparer.InvariantCultureIgnoreCase)
        {
        }

        /// <summary>
        /// Gets a cached value by name from the turn's context.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="key">The name of the object.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="key"/> is null.</exception>
        /// <returns>The object; or null if no service is registered by the key, or
        /// the retrieved object does not match the object type.</returns>
        public T Get<T>(string key)
        {
            AssertionHelpers.ThrowIfNull(key, nameof(key));
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(Get));

            if (TryGetValue(key, out var service))
            {
                if (service is T result)
                {
                    return result;
                }
            }

            // return null if either the key or type don't match
            return default;
        }

        /// <summary>
        /// Gets the default value by type from the turn's context.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <returns>The object; or null if no default service of the type is registered.</returns>
        /// <remarks>The default service key is the <see cref="Type.FullName"/> of the object type.</remarks>
        public T Get<T>()
            where T : class
        {
            return Get<T>(typeof(T).FullName);
        }

        /// <summary>
        /// Set a value to the turn's context.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="key">The name of the object.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="key"/> or <paramref name="value"/>is null.</exception>
        public void Set<T>(string key, T value)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(Set));
            AssertionHelpers.ThrowIfNull(value, nameof(value));

            this[key] = value;
        }

        /// <summary>
        /// Set a value to the turn's context.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="value">The value to add.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="value"/>is null.</exception>
        public void Set<T>(T value)
            where T : class
        {
            Set(typeof(T).FullName, value);
        }

        public bool Has(string key)
        {
            return ContainsKey(key);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Frees resources if the disposing parameter is set to true.
        /// </summary>
        /// <param name="disposing">Boolean value that indicates if freeing resources should be performed.</param>
        protected virtual void Dispose(bool disposing)
        {
            _disposed = true;
        }
    }
}
