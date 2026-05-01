// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Dialogs.Prompts;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class DialogContextTests
    {
        private readonly Mock<ITurnContext> _context = new();
        private readonly DialogContext _dialogContext;

        public DialogContextTests()
        {
            _dialogContext = new(new DialogSet(), _context.Object, new DialogState());
        }

        [Fact]
        public async Task BeginDialogAsync_ShouldThrowOnNullDialogId()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _dialogContext.BeginDialogAsync(null));
        }

        [Fact]
        public async Task BeginDialogAsync_ShouldThrowOnEmptyDialogId()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _dialogContext.BeginDialogAsync(string.Empty));
        }

        [Fact]
        public async Task BeginDialogAsync_ShouldThrowOnInexistingDialog()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _dialogContext.BeginDialogAsync("A"));
        }

        [Fact]
        public async Task PromptAsync_ShouldThrowOnNullDialogId()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _dialogContext.PromptAsync(null, new PromptOptions()));
        }

        [Fact]
        public async Task PromptAsync_ShouldThrowOnEmptyDialogId()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _dialogContext.PromptAsync(string.Empty, new PromptOptions()));
        }

        [Fact]
        public async Task PromptAsync_ShouldThrowOnNullOptions()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _dialogContext.PromptAsync("A", null));
        }

        [Fact]
        public async Task ContinueDialogAsync_ShouldThrowOnNullDialog()
        {
            _dialogContext.Stack.AddRange([
                new DialogInstance{ Id = "A" },
                new DialogInstance{ Id = "B" }
            ]);

            _context.SetupGet(e => e.StackState)
                .Returns([])
                .Verifiable(Times.Exactly(2));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _dialogContext.ContinueDialogAsync());
            Mock.Verify(_context);
        }

        [Fact]
        public async Task EndDialogAsync_ShouldThrowOnResultCancellationToken()
        {
            _dialogContext.Stack.Add(new DialogInstance { Id = "A" });

            await Assert.ThrowsAsync<ArgumentException>(() => _dialogContext.EndDialogAsync(CancellationToken.None));
        }

        [Fact]
        public async Task EndDialogAsync_ShouldThrowOnNullDialog()
        {
            _dialogContext.Stack.AddRange([
                new DialogInstance{ Id = "A" },
                new DialogInstance{ Id = "B" }
            ]);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _dialogContext.EndDialogAsync());
        }

        [Fact]
        public async Task CancelAllDialogsAsync_ShouldThrowOnEventValueCancellationToken()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _dialogContext.CancelAllDialogsAsync(false, "event", CancellationToken.None));
        }

        [Fact]
        public async Task ReplaceDialogAsync_ShouldThrowOnOptionsCancellationToken()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _dialogContext.ReplaceDialogAsync("A", CancellationToken.None));
        }

        [Fact]
        public async Task RepromptDialogAsync_ShouldThrowOnNullDialog()
        {
            _dialogContext.Stack.Add(new DialogInstance { Id = "A" });

            await Assert.ThrowsAsync<InvalidOperationException>(() => _dialogContext.RepromptDialogAsync());
        }

        [Fact]
        public void FindDialog_ShouldReturnFromParentDialog()
        {
            var parentDialog = new WaterfallDialog("B");
            var parentContainer = new ComponentDialog("A");
            parentContainer.AddDialog(parentDialog);
            _dialogContext.Dialogs.Add(parentContainer);
            _dialogContext.Stack.Add(new DialogInstance { Id = "A" });
            var childContext = new DialogContext(new DialogSet(), _dialogContext, new DialogState());
            childContext.Stack.Add(new DialogInstance { Id = "C" });

            var dialog = childContext.FindDialog("B");

            Assert.Equal(parentDialog, dialog);
        }

        [Fact]
        public void GetLocale_ShouldReturnDefaultLocaleWhenNotFound()
        {
            _context.SetupGet(e => e.Activity)
                .Throws(new CultureNotFoundException())
                .Verifiable(Times.Once);

            var locale = _dialogContext.GetLocale();

            Assert.Equal(Thread.CurrentThread.CurrentCulture.Name, locale);
            Mock.Verify(_context);
        }

        [Fact]
        public void GetLocale_ShouldReturnDefaultLocaleWhenIsEmpty()
        {
            _context.SetupGet(e => e.Activity)
                .Returns(new Activity { Locale = string.Empty })
                .Verifiable(Times.Once);

            var locale = _dialogContext.GetLocale();

            Assert.Equal(Thread.CurrentThread.CurrentCulture.Name, locale);
            Mock.Verify(_context);
        }

        [Fact]
        public void GetLocale_ShouldReturnLocaleFromActivity()
        {
            _context.SetupGet(e => e.Activity)
                .Returns(new Activity { Locale = "es-ES" })
                .Verifiable(Times.Once);

            var locale = _dialogContext.GetLocale();

            Assert.Equal("es-ES", locale);
            Mock.Verify(_context);
        }
    }
}
