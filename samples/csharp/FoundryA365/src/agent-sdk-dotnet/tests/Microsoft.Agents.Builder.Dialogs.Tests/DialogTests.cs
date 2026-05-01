// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Dialogs.Debugging;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    [Collection("DebugSupport.SourceMap")]
    public class DialogTests
    {
        private readonly Mock<DialogContext> _dialogContext = new(new DialogSet(), new Mock<ITurnContext>().Object, new DialogState());

        [Fact]
        public async Task ContinueDialogAsync_ShouldReturnComplete()
        {
            var dialog = new MockDialog("A");

            var result = await dialog.ContinueDialogAsync(_dialogContext.Object);

            Assert.Equal(DialogTurnStatus.Complete, result.Status);
        }

        [Fact]
        public async Task ResumeDialogAsync_ShouldReturnComplete()
        {
            var dialog = new MockDialog("A");

            var result = await dialog.ResumeDialogAsync(_dialogContext.Object, DialogReason.BeginCalled, null);

            Assert.Equal(DialogTurnStatus.Complete, result.Status);
        }

        [Fact]
        public async Task ResumeDialogAsync_ShouldThrowOnResultCancellationToken()
        {
            var dialog = new MockDialog("A");

            await Assert.ThrowsAsync<ArgumentException>(() => dialog.ResumeDialogAsync(_dialogContext.Object, DialogReason.BeginCalled, CancellationToken.None));
        }

        [Fact]
        public async Task RegisterSourceLocation_ShouldSetSourceMap()
        {
            var dialog = new MockDialog("A");

            var oldSourceMap = DebugSupport.SourceMap;
            DebugSupport.SourceMap = new SourceMap();
            await dialog.BeginDialogAsync(_dialogContext.Object);
            DebugSupport.SourceMap.TryGetValue(dialog, out var range);

            Assert.Equal("path", range.Path);
            Assert.Equal(1, range.StartPoint.LineIndex);
            Assert.Equal(2, range.EndPoint.LineIndex);

            DebugSupport.SourceMap = oldSourceMap;
        }

        private class MockDialog(string id) : Dialog(id)
        {
            public override Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
            {
                RegisterSourceLocation("path", 1);
                return Task.FromResult(new DialogTurnResult(DialogTurnStatus.Complete));
            }
        }
    }
}
