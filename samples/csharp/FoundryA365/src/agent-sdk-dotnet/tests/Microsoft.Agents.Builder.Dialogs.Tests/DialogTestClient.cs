// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Compat;
using System.Security.Claims;
using Microsoft.Agents.Builder.Testing;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    /// <summary>
    /// A client to for testing dialogs in isolation.
    /// </summary>
    public class DialogTestClient
    {
        private readonly AgentCallbackHandler _callback;
        private readonly TestAdapter _testAdapter;

        /// <summary>
        /// Initializes a new instance of the <see cref="DialogTestClient"/> class.
        /// </summary>
        /// <param name="channelId">
        /// The channelId (see <see cref="Channels"/>) to be used for the test.
        /// Use <see cref="Channels.Test"/> if you are uncertain of the channel you are targeting.
        /// Otherwise, it is recommended that you use the id for the channel(s) your bot will be using.
        /// Consider writing a test case for each channel.
        /// </param>
        /// <param name="targetDialog">The dialog to be tested. This will be the root dialog for the test client.</param>
        /// <param name="initialDialogOptions">(Optional) additional argument(s) to pass to the dialog being started.</param>
        /// <param name="middlewares">(Optional) A list of middlewares to be added to the test adapter.</param>
        /// <param name="conversationState">(Optional) A <see cref="ConversationState"/> to use in the test client.</param>
        /// <param name="contextClaims">(Optional) Claims to use for TurnContext.Identity</param>
        public DialogTestClient(string channelId, Dialog targetDialog, object initialDialogOptions = null, IEnumerable<IMiddleware> middlewares = null, ConversationState conversationState = null, ClaimsIdentity contextClaims = null)
        {
            ConversationState = conversationState ?? new ConversationState(new MemoryStorage());
            _testAdapter = new TestAdapter(channelId)
            {
                ClaimsIdentity = contextClaims
            };
            _testAdapter.Use(new AutoSaveStateMiddleware(true, ConversationState));

            AddUserMiddlewares(middlewares);

            _callback = GetDefaultCallback(targetDialog, initialDialogOptions);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DialogTestClient"/> class.
        /// </summary>
        /// <param name="testAdapter">The <see cref="TestAdapter"/> to use.</param>
        /// <param name="targetDialog">The dialog to be tested. This will be the root dialog for the test client.</param>
        /// <param name="initialDialogOptions">(Optional) additional argument(s) to pass to the dialog being started.</param>
        /// <param name="middlewares">(Optional) A list of middlewares to be added to the test adapter.</param>
        /// <param name="conversationState">(Optional) A <see cref="ConversationState"/> to use in the test client.</param>
        public DialogTestClient(TestAdapter testAdapter, Dialog targetDialog, object initialDialogOptions = null, IEnumerable<IMiddleware> middlewares = null, ConversationState conversationState = null, ClaimsIdentity contextClaims = null)
        {
            ConversationState = conversationState ?? new ConversationState(new MemoryStorage());
            _testAdapter = testAdapter.Use(new AutoSaveStateMiddleware(true, ConversationState));
            _testAdapter.ClaimsIdentity = contextClaims;

            AddUserMiddlewares(middlewares);

            _callback = GetDefaultCallback(targetDialog, initialDialogOptions);
        }

        /// <summary>
        /// Gets a reference for the <see cref="DialogContext"/>.
        /// </summary>
        /// <value>
        /// A reference for the <see cref="DialogContext"/>.
        /// </value>
        /// <remarks>
        /// This property will be null until at least one activity is sent to <see cref="DialogTestClient"/>.
        /// </remarks>
        public DialogContext DialogContext { get; private set; }

        /// <summary>
        /// Gets the latest <see cref="DialogTurnResult"/> for the dialog being tested.
        /// </summary>
        /// <value>A <see cref="DialogTurnResult"/> instance with the result of the last turn.</value>
        public DialogTurnResult DialogTurnResult { get; private set; }

        /// <summary>
        /// Gets the latest <see cref="ConversationState"/> for <see cref="DialogTestClient"/>.
        /// </summary>
        /// <value>A <see cref="ConversationState"/> instance for the current test client.</value>
        public ConversationState ConversationState { get; }

        /// <summary>
        /// Sends an <see cref="Activity"/> to the target dialog.
        /// </summary>
        /// <param name="activity">The activity to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        /// <typeparam name="T">An <see cref="Activity"/> derived type.</typeparam>
        public virtual async Task<T> SendActivityAsync<T>(Activity activity, CancellationToken cancellationToken = default)
            where T : Activity
        {
            await _testAdapter.ProcessActivityAsync(activity, _callback, cancellationToken).ConfigureAwait(false);
            return GetNextReply<T>();
        }

        /// <summary>
        /// Sends a message activity to the target dialog.
        /// </summary>
        /// <param name="text">The text of the message to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        /// <typeparam name="T">An <see cref="Activity"/> derived type.</typeparam>
        public virtual async Task<T> SendActivityAsync<T>(string text, CancellationToken cancellationToken = default)
            where T : Activity
        {
            await _testAdapter.SendTextToBotAsync(text, _callback, cancellationToken).ConfigureAwait(false);
            return GetNextReply<T>();
        }

        /// <summary>
        /// Gets the next bot response.
        /// </summary>
        /// <returns>The next activity in the queue; or null, if the queue is empty.</returns>
        /// <typeparam name="T">An <see cref="Activity"/> derived type.</typeparam>
        public virtual T GetNextReply<T>()
            where T : Activity
        {
            return (T)_testAdapter.GetNextReply();
        }

        private AgentCallbackHandler GetDefaultCallback(Dialog targetDialog, object initialDialogOptions) =>
            async (turnContext, cancellationToken) =>
            {
                // Ensure dialog state is created and pass it to DialogSet.
                await ConversationState.LoadAsync(turnContext).ConfigureAwait(false);
                var dialogState = ConversationState.GetValue("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(targetDialog);

                DialogContext = await dialogs.CreateContextAsync(turnContext, cancellationToken).ConfigureAwait(false);
                DialogTurnResult = await DialogContext.ContinueDialogAsync(cancellationToken).ConfigureAwait(false);
                switch (DialogTurnResult.Status)
                {
                    case DialogTurnStatus.Empty:
                        DialogTurnResult = await DialogContext.BeginDialogAsync(targetDialog.Id, initialDialogOptions, cancellationToken).ConfigureAwait(false);
                        break;
                    case DialogTurnStatus.Complete:
                    {
                        // Dialog has ended
                        break;
                    }
                }
            };

        private void AddUserMiddlewares(IEnumerable<IMiddleware> middlewares)
        {
            if (middlewares != null)
            {
                foreach (var middleware in middlewares)
                {
                    _testAdapter.Use(middleware);
                }
            }
        }
    }
}
