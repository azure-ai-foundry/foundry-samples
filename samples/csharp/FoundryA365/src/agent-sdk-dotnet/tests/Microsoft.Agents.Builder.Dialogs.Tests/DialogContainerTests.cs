// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class DialogContainerTests
    {

        private readonly TestContainer _container = new();
        private readonly Mock<ITurnContext> _context = new();
        private readonly Mock<DialogContext> _dialogContext;
        private readonly Mock<WaterfallDialog> _dialog = new("A", null);

        public DialogContainerTests()
        {
            _dialogContext = new(new DialogSet(), _context.Object, new DialogState());
        }

        [Fact]
        public void DialogContainer_GetVersion()
        {
            var ds = new TestContainer();
            var version1 = ds.GetInternalVersion_Test();
            Assert.NotNull(version1);

            var ds2 = new TestContainer();
            var version2 = ds.GetInternalVersion_Test();
            Assert.NotNull(version2);
            Assert.Equal(version1, version2);

            ds2.Dialogs.Add(new LamdaDialog((dc, ct) => null) { Id = "A" });
            var version3 = ds2.GetInternalVersion_Test();
            Assert.NotNull(version3);
            Assert.NotEqual(version2, version3);

            var version4 = ds2.GetInternalVersion_Test();
            Assert.NotNull(version3);
            Assert.Equal(version3, version4);

            var ds3 = new TestContainer();
            ds3.Dialogs.Add(new LamdaDialog((dc, ct) => null) { Id = "A" });

            var version5 = ds3.GetInternalVersion_Test();
            Assert.NotNull(version5);
            Assert.Equal(version5, version4);

            ds3.Property = "foobar";
            var version6 = ds3.GetInternalVersion_Test();
            Assert.NotNull(version6);
            Assert.NotEqual(version6, version5);

            var ds4 = new TestContainer()
            {
                Property = "foobar"
            };

            ds4.Dialogs.Add(new LamdaDialog((dc, ct) => null) { Id = "A" });
            var version7 = ds4.GetInternalVersion_Test();
            Assert.NotNull(version7);
            Assert.Equal(version7, version6);
        }

        [Fact]
        public void FindDialog_ShouldReturnDialog()
        {
            _container.Dialogs.Add(_dialog.Object);
            var actual = _container.FindDialog("A", null);

            Assert.Equal(_dialog.Object, actual);
        }

        [Fact]
        public void FindDialog_ShouldReturnDialogFromDialogContext()
        {
            _dialogContext.Object.Dialogs.Add(_dialog.Object);
            var actual = _container.FindDialog("A", _dialogContext.Object);

            Assert.Equal(_dialog.Object, actual);
        }

        [Fact]
        public async Task OnDialogEventAsync_ShouldTriggerUnhandledVersionChanged()
        {
            _dialogContext.Object.Stack.Add(new DialogInstance { Id = "A" });

            _context.Setup(e => e.TraceActivityAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse())
                .Verifiable(Times.Once);

            var actual = await _container.OnDialogEventAsync(_dialogContext.Object, new DialogEvent { Name = DialogEvents.VersionChanged }, CancellationToken.None);

            Assert.False(actual);
            Mock.Verify(_context);
        }

        [Fact]
        public async Task CheckForVersionChangeAsync_ShouldEmitVersionChangedEvent()
        {
            _dialogContext.Object.Stack.Add(new DialogInstance { Id = "A", Version = "1" });

            _context.SetupGet(e => e.StackState)
                .Returns([]);
            _context.SetupGet(e => e.Services)
                .Returns([])
                .Verifiable(Times.Once);
            _dialog.Setup(e => e.OnDialogEventAsync(It.IsAny<DialogContext>(), It.IsAny<DialogEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false)
                .Verifiable(Times.Once);

            _dialogContext.Object.Dialogs.Add(_dialog.Object);
            await _container.CheckForVersionChangeAsync_Test(_dialogContext.Object);

            Mock.Verify(_context, _dialog);
        }
    }

    public class TestContainer : DialogContainer
    {
        public string Property { get; set; }

        public override Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
        {
            return dc.EndDialogAsync();
        }

        public override DialogContext CreateChildContext(DialogContext dc)
        {
            return dc;
        }

        public Task CheckForVersionChangeAsync_Test(DialogContext dc)
        {
            return CheckForVersionChangeAsync(dc);
        }

        public string GetInternalVersion_Test()
        {
            return GetInternalVersion();
        }

        protected override string GetInternalVersion()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(base.GetInternalVersion());
            sb.Append(Property ?? string.Empty);

            return StringUtils.Hash(sb.ToString());
        }
    }
}
