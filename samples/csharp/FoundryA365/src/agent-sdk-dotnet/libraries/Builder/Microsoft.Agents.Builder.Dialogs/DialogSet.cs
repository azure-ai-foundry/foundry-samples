// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.Builder.Dialogs
{
    /// <summary>
    /// A collection of <see cref="Dialog"/> objects that can all call each other.
    /// </summary>
    public class DialogSet
    {
        private readonly Dictionary<string, Dialog> _dialogs = new Dictionary<string, Dialog>();
        private readonly DialogState _dialogState;
        private readonly IStatePropertyAccessor<DialogState> _dialogStateProperty;
        private string _version;

        /// <summary>
        /// Initializes a new instance of the <see cref="DialogSet"/> class.
        /// </summary>
        /// <param name="dialogState">The state property accessor with which to manage the stack for
        /// this dialog set.</param>
        /// <remarks>To start and control the dialogs in this dialog set, create a <see cref="DialogContext"/>
        /// and use its methods to start, continue, or end dialogs. To create a dialog context,
        /// call <see cref="CreateContextAsync(ITurnContext, CancellationToken)"/>.
        /// </remarks>
        public DialogSet(DialogState dialogState)
        {
            _dialogState = dialogState ?? throw new ArgumentNullException(nameof(dialogState));
        }

        [Obsolete("Use DialogSet(DialogState)")]
        public DialogSet(IStatePropertyAccessor<DialogState> dialogStateProperty)
        {
            _dialogStateProperty = dialogStateProperty ?? throw new ArgumentNullException(nameof(dialogStateProperty));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DialogSet"/> class with null <see cref="DialogState"/>.
        /// </summary>
        public DialogSet()
        {
            _dialogState = null;
        }

        /// <summary>
        /// Gets a unique string which represents the combined versions of all dialogs in this this dialogset.  
        /// </summary>
        /// <returns>Version will change when any of the child dialogs version changes.</returns>
        public virtual string GetVersion()
        {
            if (_version == null)
            {
                var sb = new StringBuilder();
                foreach (var dialog in _dialogs)
                {
                    var v = _dialogs[dialog.Key].GetVersion();
                    if (v != null)
                    {
                        sb.Append(v);
                    }
                }

                _version = StringUtils.Hash(sb.ToString());
            }

            return _version;
        }

        /// <summary>
        /// Adds a new dialog to the set and returns the set to allow fluent chaining.
        /// If the Dialog.Id being added already exists in the set, the dialogs id will be updated to 
        /// include a suffix which makes it unique. So adding 2 dialogs named "duplicate" to the set
        /// would result in the first one having an id of "duplicate" and the second one having an id
        /// of "duplicate2".
        /// </summary>
        /// <param name="dialog">The dialog to add.</param>
        /// <returns>The dialog set after the operation is complete.</returns>
        /// <remarks>The added dialog's <see cref="Dialog.TelemetryClient"/> is set to the
        /// <see cref="TelemetryClient"/> of the dialog set.</remarks>
        public DialogSet Add(Dialog dialog)
        {
            // Ensure new version hash is computed
            _version = null;

            AssertionHelpers.ThrowIfNull(dialog, nameof(dialog));

            if (_dialogs.TryGetValue(dialog.Id, out Dialog dialogValue))
            {
                // If we are trying to add the same exact instance, it's not a name collision.
                // No operation required since the instance is already in the dialog set.
                if (dialogValue == dialog)
                {
                    return this;
                }

                // If we are adding a new dialog with a conflicting name, add a suffix to avoid
                // dialog name collisions.
                var nextSuffix = 2;

                while (true)
                {
                    var suffixId = dialog.Id + nextSuffix;

                    if (!_dialogs.ContainsKey(suffixId))
                    {
                        dialog.Id = suffixId;
                        break;
                    }

                    nextSuffix++;
                }
            }

            _dialogs[dialog.Id] = dialog;

            return this;
        }

        /// <summary>
        /// Creates a <see cref="DialogContext"/> which can be used to work with the dialogs in the
        /// <see cref="DialogSet"/>.
        /// </summary>
        /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>If the task is successful, the result contains the created <see cref="DialogContext"/>.
        /// </remarks>
        public async Task<DialogContext> CreateContextAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            if (_dialogState == null && _dialogStateProperty == null)
            {
                // Note: This shouldn't ever trigger, as the _dialogState is set in the constructor and validated there.
                throw new InvalidOperationException("DialogSet.CreateContextAsync(): DialogSet created with a null DialogState.");
            }

            var state = _dialogState;

            if (_dialogState == null)
            {
                // Back-compat: Load/initialize dialog state using IStatePropertyAccessor
                state = await _dialogStateProperty.GetAsync(turnContext, () => { return new DialogState(); }, cancellationToken).ConfigureAwait(false);
            }

            // Create and return context
            return new DialogContext(this, turnContext, state);
        }

        /// <summary>
        /// Searches the current <see cref="DialogSet"/> for a <see cref="Dialog"/> by its ID.
        /// </summary>
        /// <param name="dialogId">ID of the dialog to search for.</param>
        /// <returns>The dialog if found; otherwise <c>null</c>.</returns>
        public Dialog Find(string dialogId)
        {
            if (string.IsNullOrWhiteSpace(dialogId))
            {
                throw new ArgumentNullException(nameof(dialogId));
            }

            if (_dialogs.TryGetValue(dialogId, out var result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Gets the Dialogs of the set.
        /// </summary>
        /// <returns>A collection of <see cref="Dialog"/>.</returns>
        public IEnumerable<Dialog> GetDialogs()
        {
            return _dialogs.Values;
        }
    }
}
