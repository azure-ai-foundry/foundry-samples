// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Dialogs.Prompts;

namespace Microsoft.Agents.Builder.Dialogs
{
    /// <summary>
    /// Indicates in which a dialog-related method is being called.
    /// </summary>
    public enum DialogReason
    {
        /// <summary>
        /// A dialog was started.
        /// </summary>
        /// <seealso cref="Microsoft.Agents.Builder.Dialogs.DialogContext.BeginDialogAsync(string, object, System.Threading.CancellationToken)"/>
        /// <seealso cref="Microsoft.Agents.Builder.Dialogs.DialogContext.PromptAsync(string, Microsoft.Agents.Builder.Dialogs.Prompts.PromptOptions, System.Threading.CancellationToken)"/>
        BeginCalled,

        /// <summary>
        /// A dialog was continued.
        /// </summary>
        /// <seealso cref="Microsoft.Agents.Builder.Dialogs.DialogContext.ContinueDialogAsync(System.Threading.CancellationToken)"/>
        ContinueCalled,

        /// <summary>
        /// A dialog was ended normally.
        /// </summary>
        /// <seealso cref="Microsoft.Agents.Builder.Dialogs.DialogContext.EndDialogAsync(object, System.Threading.CancellationToken)"/>
        EndCalled,

        /// <summary>
        /// A dialog was ending because it was replaced.
        /// </summary>
        /// <seealso cref="Microsoft.Agents.Builder.Dialogs.DialogContext.ReplaceDialogAsync(string, object, System.Threading.CancellationToken)"/>
        ReplaceCalled,

        /// <summary>
        /// A dialog was canceled.
        /// </summary>
        /// <seealso cref="Microsoft.Agents.Builder.Dialogs.DialogContext.CancelAllDialogsAsync(System.Threading.CancellationToken)"/>
        CancelCalled,

        /// <summary>
        /// A preceding step of the dialog was skipped.
        /// </summary>
        /// <seealso cref="Microsoft.Agents.Builder.Dialogs.WaterfallStepContext.NextAsync(object, System.Threading.CancellationToken)"/>
        NextCalled,
    }
}
