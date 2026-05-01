// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.State
{
    public class TempState : IAgentState
    {
        /// <summary>
        /// Name of the input files key
        /// </summary>
        public const string InputFilesKey = "inputFiles";

        /// <summary>
        /// Name of the auth tokens property.
        /// </summary>
        private const string AuthTokenKey = "authTokens";

        public static readonly string ScopeName = "temp";
        private readonly Dictionary<string, object> _state = [];

        public string Name => ScopeName;

        /// <summary>
        /// Downloaded files included in the Activity
        /// </summary>
        public IList<InputFile> InputFiles
        {
            get => GetValue<IList<InputFile>>(InputFilesKey, () => []);
            set => SetValue(InputFilesKey, value);
        }

        /// <summary>
        /// All tokens acquired after sign-in for current activity
        /// </summary>
        [Obsolete("Use AgentApplication.UserAuthorization.GetTurnToken(handleName)")]
        public Dictionary<string, string> AuthTokens
        {
            get => GetValue<Dictionary<string, string>>(AuthTokenKey, () => []);
            set => SetValue(AuthTokenKey, value);
        }

        public void ClearState()
        {
            _state.Clear();
        }

        public Task DeleteStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            _state.Clear();
            return Task.CompletedTask;
        }

        public bool HasValue(string name)
        {
            return _state.ContainsKey(name);
        }

        public void DeleteValue(string name)
        {
            _state.Remove(name);
        }

        public T GetValue<T>(string name, Func<T> defaultValueFactory = null)
        {
            if (!_state.TryGetValue(name, out var value))
            {
                if (defaultValueFactory != null)
                {
                    value = defaultValueFactory();
                    SetValue(name, value);
                }
            }

            return (T) value;
        }

        public void SetValue<T>(string name, T value)
        {
            _state[name] = value;
        }

        public T GetValue<T>()
        {
            return GetValue<T>(typeof(T).FullName);
        }

        public void SetValue<T>(T value)
        {
            SetValue(typeof(T).FullName, value);
        }

        public bool IsLoaded()
        {
            return true;
        }

        public Task LoadAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
