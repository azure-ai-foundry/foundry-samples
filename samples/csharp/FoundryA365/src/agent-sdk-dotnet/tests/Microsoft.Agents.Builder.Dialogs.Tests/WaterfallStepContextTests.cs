// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Xunit;
using Moq;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class WaterfallStepContextTests
    {
        private static readonly Mock<ITurnContext> _context = new();
        private static readonly Mock<DialogContext> _dialogContext = new(new DialogSet(), _context.Object, new DialogState());
        private static readonly Mock<WaterfallDialog> _dialog = new("id", null);
        private static readonly WaterfallStepContext _stepContext = new(_dialog.Object, _dialogContext.Object, null, null, 0, DialogReason.BeginCalled);

        [Fact]
        public async Task NextAsync_ShouldThrowOnResultCancellationToken()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _stepContext.NextAsync(CancellationToken.None));
        }

        [Fact]
        public async Task NextAsync_ShouldThrowOnNextCall()
        {
            _dialog.Setup(e => e.ResumeDialogAsync(It.IsAny<DialogContext>(), It.IsAny<DialogReason>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DialogTurnResult(DialogTurnStatus.Complete))
                .Verifiable(Times.Once);

            await _stepContext.NextAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => _stepContext.NextAsync());
            Mock.Verify(_dialog);
        }
    }
}
