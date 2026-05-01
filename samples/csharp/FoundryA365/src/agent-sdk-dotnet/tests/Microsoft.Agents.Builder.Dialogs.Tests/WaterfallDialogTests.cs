// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Xunit;
using Moq;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class WaterfallDialogTests
    {
        private static readonly Mock<ITurnContext> _context = new();
        private static readonly Mock<DialogContext> _dialogContext = new(new DialogSet(), _context.Object, new DialogState());
        private static readonly MockWaterfallDialog _dialog = new("A");

        [Fact]
        public async Task BeginDialogAsync_ShouldThrowOnOptionsCancellationToken()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _dialog.BeginDialogAsync(_dialogContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task BeginDialogAsync_ShouldThrowOnNullDialogContext()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _dialog.BeginDialogAsync(null));
        }

        [Fact]
        public async Task ContinueDialogAsync_ShouldThrowOnNullDialogContext()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _dialog.ContinueDialogAsync(null));
        }

        [Fact]
        public async Task ContinueDialogAsync_ShouldReturnEndOfTurnOnNonMessageActivity()
        {
            _context.SetupGet(e => e.Activity.Type)
                .Returns(ActivityTypes.Event)
                .Verifiable(Times.Once);

            var result = await _dialog.ContinueDialogAsync(_dialogContext.Object);

            Assert.Equal(Dialog.EndOfTurn, result);
            Mock.Verify(_context);
        }

        [Fact]
        public async Task ResumeDialogAsync_ShouldThrowOnNullDialogContext()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _dialog.ResumeDialogAsync(null, DialogReason.BeginCalled, "result"));
        }

        [Fact]
        public async Task RunStepAsync_ShouldThrowOnNullDialogContext()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _dialog.RunStepAsync_Test(null, 0, DialogReason.BeginCalled, "result", CancellationToken.None));
        }

        private class MockWaterfallDialog(string dialogId, IEnumerable<WaterfallStep> actions = null) : WaterfallDialog(dialogId, actions)
        {
            public Task<DialogTurnResult> RunStepAsync_Test(DialogContext dc, int index, DialogReason reason, object result, CancellationToken cancellationToken)
            {
                return RunStepAsync(dc, index, reason, result, cancellationToken);
            }
        }
    }
}
