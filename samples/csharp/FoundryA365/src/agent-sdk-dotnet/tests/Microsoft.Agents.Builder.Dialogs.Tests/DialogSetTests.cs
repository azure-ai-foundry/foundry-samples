// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class DialogSetTests
    {
        [Fact]
        public void DialogSet_ConstructorValid()
        {
            var convoState = new ConversationState(new MemoryStorage());
            new DialogSet(new DialogState());
        }

        [Fact]
        public void DialogSet_ConstructorNullProperty()
        {
            Assert.Throws<ArgumentNullException>(() => new DialogSet((DialogState)null));
        }

        [Fact]
        public async Task DialogSet_CreateContextAsync()
        {
            var convoState = new ConversationState(new MemoryStorage());
            var ds = new DialogSet(new DialogState());
            var context = TestUtilities.CreateEmptyContext();
            await ds.CreateContextAsync(context);
        }

        [Fact]
        public async Task DialogSet_NullCreateContextAsync()
        {
            var convoState = new ConversationState(new MemoryStorage());
            var ds = new DialogSet(new DialogState());
            var context = TestUtilities.CreateEmptyContext();
            await ds.CreateContextAsync(context);
        }

        [Fact]
        public async Task DialogSet_AddWorks()
        {
            var convoState = new ConversationState(new MemoryStorage());
            var ds = new DialogSet(new DialogState())
                .Add(new WaterfallDialog("A"))
                .Add(new WaterfallDialog("B"));
            Assert.NotNull(ds.Find("A"));
            Assert.NotNull(ds.Find("B"));
            Assert.Null(ds.Find("C"));
            await Task.CompletedTask;
        }

        [Fact]
        public void DialogSet_GetVersion()
        {
            var ds = new DialogSet();
            var version1 = ds.GetVersion();
            Assert.NotNull(version1);

            var ds2 = new DialogSet();
            var version2 = ds.GetVersion();
            Assert.NotNull(version2);
            Assert.Equal(version1, version2);

            ds2.Add(new LamdaDialog((dc, ct) => null) { Id = "A" });
            var version3 = ds2.GetVersion();
            Assert.NotNull(version3);
            Assert.NotEqual(version2, version3);

            var version4 = ds2.GetVersion();
            Assert.NotNull(version3);
            Assert.Equal(version3, version4);

            var ds3 = new DialogSet()
                .Add(new LamdaDialog((dc, ct) => null) { Id = "A" });

            var version5 = ds3.GetVersion();
            Assert.NotNull(version5);
            Assert.Equal(version5, version4);
        }

        [Fact]
        public void Add_ShouldThrowOnNullDialog()
        {
            var dialogSet = new DialogSet(new DialogState());

            Assert.Throws<ArgumentNullException>(() => dialogSet.Add(null));
        }

        [Fact]
        public void Add_ShouldUseSameInstance()
        {
            var dialog = new WaterfallDialog("A");
            var dialogSet = new DialogSet(new DialogState());

            dialogSet.Add(dialog);
            dialogSet.Add(dialog);

            Assert.Single(dialogSet.GetDialogs());
        }

        [Fact]
        public void Add_ShouldApplySuffixId()
        {
            var name = "A";
            var dialogSet = new DialogSet(new DialogState());

            dialogSet.Add(new WaterfallDialog(name));
            dialogSet.Add(new WaterfallDialog(name));
            dialogSet.Add(new WaterfallDialog(name));

            var dialogs = dialogSet.GetDialogs();
            Assert.Equal(name, dialogs.ElementAt(0).Id);
            Assert.Equal($"{name}2", dialogs.ElementAt(1).Id);
            Assert.Equal($"{name}3", dialogs.ElementAt(2).Id);
        }

        [Fact]
        public async Task CreateContextAsync_ShouldThrowOnNullDialogState()
        {
            var dialogState = new DialogState([
                new DialogInstance { Id = "A" }
            ]);
            var property = new Mock<IStatePropertyAccessor<DialogState>>();

            property.Setup(e => e.GetAsync(It.IsAny<ITurnContext>(), It.IsAny<Func<DialogState>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dialogState)
                .Verifiable(Times.Once);

#pragma warning disable CS0618 // Type or member is obsolete
            var dialogSet = new DialogSet(property.Object);
#pragma warning restore CS0618 // Type or member is obsolete
            var context = new TurnContext(new TestAdapter(), new Activity());

            var dialogContext = await dialogSet.CreateContextAsync(context);

            Assert.Equal(dialogContext.Stack, dialogState.DialogStack);
            Mock.Verify(property);
        }

        [Fact]
        public async Task CreateContextAsync_ShouldThrowOnNullDialogStateAndProperty()
        {
            var dialogSet = new DialogSet();
            var context = new TurnContext(new TestAdapter(), new Activity());

            await Assert.ThrowsAsync<InvalidOperationException>(() => dialogSet.CreateContextAsync(context));
        }

        [Fact]
        public void Find_ShouldThrowOnNullOrEmptyDialogId()
        {
            var dialogSet = new DialogSet();

            Assert.Throws<ArgumentNullException>(() => dialogSet.Find(null));
            Assert.Throws<ArgumentNullException>(() => dialogSet.Find(string.Empty));
        }
    }
}
