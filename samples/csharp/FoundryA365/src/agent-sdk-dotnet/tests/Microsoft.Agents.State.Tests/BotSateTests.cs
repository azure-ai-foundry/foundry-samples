// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using Xunit;

namespace Microsoft.Agents.State.Tests
{
    public class BotStateTests
    {
        [Fact]
        public async Task State_EmptyName()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            await userState.LoadAsync(context);

            // Act
            Assert.Throws<ArgumentException>(() => userState.GetValue<string>(string.Empty, () => string.Empty));
        }

        [Fact]
        public async Task State_NullName()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            await userState.LoadAsync(context);

            // Act
            Assert.Throws<ArgumentNullException>(() => userState.GetValue<string>(null, () => string.Empty));
        }

        [Fact]
        public void State_WithDefaultBotState()
        {
            var storage = new MemoryStorage();

            var turnState = new TurnState(storage);

            Assert.IsAssignableFrom<ConversationState>(turnState.Conversation);
            Assert.IsAssignableFrom<UserState>(turnState.User);
            Assert.IsAssignableFrom<TempState>(turnState.Temp);
            Assert.Throws<ArgumentException>(() => turnState.Private);

            turnState = new TurnState(storage, new PrivateConversationState(storage));
            Assert.IsAssignableFrom<ConversationState>(turnState.Conversation);
            Assert.IsAssignableFrom<UserState>(turnState.User);
            Assert.IsAssignableFrom<TempState>(turnState.Temp);
            Assert.IsAssignableFrom<PrivateConversationState>(turnState.GetScope<PrivateConversationState>());

            turnState = new TurnState(new UserState(storage), new ConversationState(storage), new TempState());
            Assert.IsAssignableFrom<ConversationState>(turnState.Conversation);
            Assert.IsAssignableFrom<UserState>(turnState.User);
            Assert.IsAssignableFrom<TempState>(turnState.Temp);
            Assert.Throws<ArgumentException>(() => turnState.Private);
        }

        [Fact]
        public async Task State_WriteAsyncStoreItem()
        {
            var dictionary = new Dictionary<string, JsonObject>();
            var memory = new MemoryStorage(dictionary: dictionary);

            var changes = new Dictionary<string, object>()
            {
                { "customState", new CustomState() },
            };
            await memory.WriteAsync(changes, CancellationToken.None);
            var result = await memory.ReadAsync(new string[] { "customState" }, CancellationToken.None);

            Assert.Equal("0", dictionary["customState"]["ETag"].ToString());
            Assert.Equal("0", (result["customState"] as CustomState).ETag);
        }

        [Fact]
        public async Task MakeSureStorageNotCalledNoChangesAsync()
        {
            // Mock a storage provider, which counts read/writes
            var storeCount = 0;
            var readCount = 0;
            var dictionary = new Dictionary<string, object>();
            var mock = new Mock<IStorage>();
            mock.Setup(ms => ms.WriteAsync(It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => storeCount++);
            mock.Setup(ms => ms.ReadAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(result: (IDictionary<string, object>)dictionary))
                .Callback(() => readCount++);

            // Arrange
            var userState = new UserState(mock.Object);

            var context = TestUtilities.CreateEmptyContext();

            await userState.LoadAsync(context, false);

            // Act
            Assert.Equal(0, storeCount);
            await userState.SaveChangesAsync(context);
            userState.SetValue("propertyA", "hello");
            Assert.Equal(1, readCount);       // Initial save bumps count
            Assert.Equal(0, storeCount);       // Initial save bumps count
            userState.SetValue("propertyA", "there");
            Assert.Equal(0, storeCount);       // Set on property should not bump
            await userState.SaveChangesAsync(context);
            Assert.Equal(1, storeCount);       // Explicit save should bump
            var valueA = userState.GetValue("propertyA", () => string.Empty);
            Assert.Equal("there", valueA);
            Assert.Equal(1, storeCount);       // Gets should not bump
            await userState.SaveChangesAsync(context);
            Assert.Equal(1, storeCount);
            userState.DeleteValue("propertyA");   // Delete alone no bump
            Assert.Equal(1, storeCount);
            await userState.SaveChangesAsync(context);  // Save when dirty should bump
            Assert.Equal(2, storeCount);
            Assert.Equal(1, readCount);
            await userState.SaveChangesAsync(context);  // Save not dirty should not bump
            Assert.Equal(2, storeCount);
            Assert.Equal(1, readCount);
        }

        [Fact]
        public async Task State_SetNoLoad()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            await userState.LoadAsync(context, false);

            // Act
            userState.SetValue("propertyA", "hello");
        }

        [Fact]
        public async Task State_MultipleLoads()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await userState.LoadAsync(context);
            await userState.LoadAsync(context);
        }

        [Fact]
        public async Task State_GetNoLoadWithDefault()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            await userState.LoadAsync(context, false);

            // Act
            var valueA = userState.GetValue("propertyA", () => "Default!");
            Assert.Equal("Default!", valueA);
        }

        [Fact]
        public async Task State_GetNoLoadNoDefault()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            await userState.LoadAsync(context, false);

            // Act
            var valueA = userState.GetValue<string>("propertyA");

            // Assert
            Assert.Null(valueA);
        }

        [Fact]
        public async Task State_POCO_NoDefault()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            await userState.LoadAsync(context, false);

            // Act
            var value = userState.GetValue<TestPocoState>("test");

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public async Task State_bool_NoDefault()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            await userState.LoadAsync(context, false);

            // Act
            var value = userState.GetValue<bool>("test", null);

            // Assert
            Assert.False(value);
        }

        [Fact]
        public async Task State_int_NoDefault()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            await userState.LoadAsync(context, false);

            // Act
            var value = userState.GetValue<int>("test", null);

            // Assert
            Assert.Equal(0, value);
        }

        [Fact]
        public async Task State_SetAfterSave()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await userState.LoadAsync(context);
            userState.SetValue("property-a", "hello");
            userState.SetValue("property-b", "world");
            await userState.SaveChangesAsync(context);

            userState.SetValue("property-a", "hello2");
        }

        [Fact]
        public async Task State_MultipleSave()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await userState.LoadAsync(context);
            userState.SetValue("property-a", "hello");
            userState.SetValue("property-b", "world");
            await userState.SaveChangesAsync(context);

            userState.SetValue("property-a", "hello2");
            await userState.SaveChangesAsync(context);
            var valueA = userState.GetValue<string>("property-a");
            Assert.Equal("hello2", valueA);
        }

        [Fact]
        public async Task LoadSetSave()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await userState.LoadAsync(context);
            userState.SetValue("property-a", "hello");
            userState.SetValue("property-b", "world");
            await userState.SaveChangesAsync(context);

            // Assert
            var obj = dictionary["EmptyContext/users/empty@empty.context.org"];
            Assert.Equal("hello", obj["property-a"].ToString());
            Assert.Equal("world", obj["property-b"].ToString());
        }

        [Fact]
        public async Task LoadSetSaveTwice()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var context = TestUtilities.CreateEmptyContext();

            // Act
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));

            await userState.LoadAsync(context);
            userState.SetValue("property-a", "hello");
            userState.SetValue("property-b", "world");
            userState.SetValue("property-c", "test");
            await userState.SaveChangesAsync(context);

            // Assert
            var obj = dictionary["EmptyContext/users/empty@empty.context.org"];
            Assert.Equal("hello", obj["property-a"].ToString());
            Assert.Equal("world", obj["property-b"].ToString());

            // Act 2
            var userState2 = new UserState(new MemoryStorage(dictionary: dictionary));

            await userState2.LoadAsync(context);
            userState2.SetValue("property-a", "hello-2");
            userState2.SetValue("property-b", "world-2");
            await userState2.SaveChangesAsync(context);

            // Assert 2
            var obj2 = dictionary["EmptyContext/users/empty@empty.context.org"];
            Assert.Equal("hello-2", obj2["property-a"].ToString());
            Assert.Equal("world-2", obj2["property-b"].ToString());
            Assert.Equal("test", obj2["property-c"].ToString());
        }

        [Fact]
        public async Task LoadSaveDelete()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var context = TestUtilities.CreateEmptyContext();

            // Act
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));

            await userState.LoadAsync(context);
            userState.SetValue("property-a", "hello");
            userState.SetValue("property-b", "world");
            await userState.SaveChangesAsync(context);

            // Assert
            var obj = dictionary["EmptyContext/users/empty@empty.context.org"];
            Assert.Equal("hello", obj["property-a"].ToString());
            Assert.Equal("world", obj["property-b"].ToString());

            // Act 2
            var userState2 = new UserState(new MemoryStorage(dictionary: dictionary));

            await userState2.LoadAsync(context);
            userState2.SetValue("property-a", "hello-2");
            userState2.DeleteValue("property-b");
            await userState2.SaveChangesAsync(context);

            // Assert 2
            var obj2 = dictionary["EmptyContext/users/empty@empty.context.org"];
            Assert.Equal("hello-2", obj2["property-a"].ToString());
            Assert.Null(obj2["property-b"]);
        }

        [Fact]
        public async Task State_RememberIStoreItemUserState()
        {
            var userState = new UserState(new MemoryStorage());
            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_RememberIStoreItemUserState"))
                .Use(new AutoSaveStateMiddleware(userState));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                {
                    await userState.LoadAsync(context, false);
                    var state = userState.GetValue("test", () => new TestState());
                    Assert.NotNull(state);
                    switch (context.Activity.Text)
                    {
                        case "set value":
                            state.Value = "test";
                            await context.SendActivityAsync("value saved");
                            break;
                        case "get value":
                            await context.SendActivityAsync(state.Value);
                            break;
                    }
                })
                .Test("set value", "value saved")
                .Test("get value", "test")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_RememberPocoUserState()
        {
            var userState = new UserState(new MemoryStorage());
            var adapter = new TestAdapter(TestAdapter.CreateConversation("tate_RememberPocoUserState"))
                .Use(new AutoSaveStateMiddleware(userState));
            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        await userState.LoadAsync(context, false, cancellationToken);
                        var testPocoState = userState.GetValue("testPoco", () => new TestPocoState());
                        Assert.NotNull(userState);
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                testPocoState.Value = "test";
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(testPocoState.Value);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", "test")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_RememberIStoreItemConversationState()
        {
            var userState = new UserState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_RememberIStoreItemConversationState"))
                .Use(new AutoSaveStateMiddleware(userState));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        await userState.LoadAsync(context, false, cancellationToken);
                        var conversationState = userState.GetValue("test", () => new TestState());
                        Assert.NotNull(conversationState);
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                conversationState.Value = "test";
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(conversationState.Value);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", "test")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_RememberPocoConversationState()
        {
            var userState = new UserState(new MemoryStorage());
            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_RememberPocoConversationState"))
                .Use(new AutoSaveStateMiddleware(userState));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        await userState.LoadAsync(context, false, cancellationToken);
                        var conversationState = userState.GetValue("testPoco", () => new TestPocoState());
                        Assert.NotNull(conversationState);
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                conversationState.Value = "test";
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(conversationState.Value);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", "test")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_RememberPocoPrivateConversationState()
        {
            var privateConversationState = new PrivateConversationState(new MemoryStorage());
            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_RememberPocoPrivateConversationState"))
                .Use(new AutoSaveStateMiddleware(privateConversationState));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        await privateConversationState.LoadAsync(context, false);
                        var conversationState = privateConversationState.GetValue("testPoco", () => new TestPocoState());
                        Assert.NotNull(conversationState);
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                conversationState.Value = "test";
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(conversationState.Value);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", "test")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_CustomStateManagerTest()
        {
            var testGuid = Guid.NewGuid().ToString();
            var customState = new CustomKeyState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_CustomStateManagerTest"))
                .Use(new AutoSaveStateMiddleware(customState));

            await new TestFlow(adapter, async (context, cancellationToken) =>
                    {
                        await customState.LoadAsync(context, false, cancellationToken);
                        var test = customState.GetValue("test", () => new TestPocoState());
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                test.Value = testGuid;
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(test.Value);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", testGuid.ToString())
                .StartTestAsync();
        }

        [Fact]
        public async Task State_RoundTripTypedObject()
        {
            var convoState = new ConversationState(new MemoryStorage());
            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_RoundTripTypedObject"))
                .Use(new AutoSaveStateMiddleware(convoState));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        await convoState.LoadAsync(context, false, cancellationToken);
                        var conversation = convoState.GetValue("typed", () => new TypedObject());
                        Assert.NotNull(conversation);
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                conversation.Name = "test";
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(conversation.GetType().Name);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", "TypedObject")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_UseBotStateDirectly()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_UseBotStateDirectly"));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        var testState = new TestBotState(new MemoryStorage());

                        // read initial state object
                        await testState.LoadAsync(context);

                        var customState = testState.GetValue("test", () => new CustomState());

                        // this should be a 'new CustomState' as nothing is currently stored in storage
                        Assert.NotNull(customState);
                        Assert.IsType<CustomState>(customState);
                        Assert.Null(customState.CustomString);

                        // amend property and write to storage
                        customState.CustomString = "test";
                        await testState.SaveChangesAsync(context, false, cancellationToken);

                        customState.CustomString = "asdfsadf";

                        // read into context again
                        await testState.LoadAsync(context, force: true, cancellationToken);

                        customState = testState.GetValue("test", () => new CustomState());

                        // check object read from value has the correct value for CustomString
                        Assert.Equal("test", customState.CustomString);
                    })
                .Send(new Activity() { Type = ActivityTypes.ConversationUpdate })
                .StartTestAsync();
        }

        [Fact]
        public async Task UserState_BadFromThrows()
        {
            var dictionary = new Dictionary<string, JsonObject>();
            var botState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();
            context.Activity.From = null;
            await Assert.ThrowsAsync<InvalidOperationException>(() => botState.LoadAsync(context, false));
        }

        [Fact]
        public async Task ConversationState_BadConverationThrows()
        {
            var dictionary = new Dictionary<string, JsonObject>();
            var botState = new ConversationState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            context.Activity.Conversation = null;

            await Assert.ThrowsAsync<InvalidOperationException>(() => botState.LoadAsync(context, false));
        }

        [Fact]
        public async Task PrivateConversationState_BadActivityFromThrows()
        {
            var dictionary = new Dictionary<string, JsonObject>();
            var botState = new PrivateConversationState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            context.Activity.Conversation = null;
            context.Activity.From = null;

            await Assert.ThrowsAsync<InvalidOperationException>(() => botState.LoadAsync(context, false));
        }

        [Fact]
        public async Task PrivateConversationState_BadActivityConversationThrows()
        {
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new PrivateConversationState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            context.Activity.Conversation = null;

            await Assert.ThrowsAsync<InvalidOperationException>(() => userState.LoadAsync(context, false));
        }

        [Fact]
        public async Task ClearAndSave()
        {
            var turnContext = TestUtilities.CreateEmptyContext();
            turnContext.Activity.Conversation = new ConversationAccount { Id = "1234" };

            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>());

            // Turn 0
            var botState1 = new ConversationState(storage);
            await botState1.LoadAsync(turnContext, false); 
            var value1 = botState1
                .GetValue("test-name", () => new TestPocoState());
            value1.Value = "test-value";
            //await botState1.SetValue(turnContext, "test-name", value1, default);
            await botState1.SaveChangesAsync(turnContext);

            // Turn 1
            var botState2 = new ConversationState(storage);
            await botState2.LoadAsync(turnContext, false);
            value1 = botState2
                .GetValue("test-name", () => new TestPocoState { Value = "default-value" });

            Assert.Equal("test-value", value1.Value);

            // Turn 2
            var botState3 = new ConversationState(storage);
            await botState3.LoadAsync(turnContext, false);
            botState3.ClearState();
            await botState3.SaveChangesAsync(turnContext);

            // Turn 3
            var botState4 = new ConversationState(storage);
            await botState4.LoadAsync(turnContext, false);
            var value2 = (botState4
                .GetValue("test-name", () => new TestPocoState { Value = "default-value" })).Value;

            Assert.Equal("default-value", value2);
        }

        [Fact]
        public async Task BotStateDelete()
        {
            var turnContext = TestUtilities.CreateEmptyContext();
            turnContext.Activity.Conversation = new ConversationAccount { Id = "1234" };

            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>());

            // Turn 0
            var botState1 = new ConversationState(storage);
            await botState1.LoadAsync(turnContext, false);

            (botState1
                .GetValue("test-name", () => new TestPocoState())).Value = "test-value";
            await botState1.SaveChangesAsync(turnContext);

            // Turn 1
            var botState2 = new ConversationState(storage);
            await botState2.LoadAsync(turnContext, false);
            var value1 = (botState2
                .GetValue("test-name", () => new TestPocoState { Value = "default-value" })).Value;

            Assert.Equal("test-value", value1);

            // Turn 2
            var botState3 = new ConversationState(storage);
            await botState3.LoadAsync(turnContext, false);
            await botState3.DeleteStateAsync(turnContext);

            // Turn 3
            var botState4 = new ConversationState(storage);
            await botState4.LoadAsync(turnContext, false);
            var value2 = (botState4
                .GetValue("test-name", () => new TestPocoState { Value = "default-value" })).Value;

            Assert.Equal("default-value", value2);
        }

        [Fact]
        public async Task BotStateGet()
        {
            var turnContext = TestUtilities.CreateEmptyContext();
            turnContext.Activity.Conversation = new ConversationAccount { Id = "1234" };

            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>());

            // This was changed from ConversationSate to TestBotState
            // because TestBotState has a context service key
            // that is different from the name of its type
            var botState = new TestBotState(storage);
            await botState.LoadAsync(turnContext, false);
            (botState
                .GetValue("test-name", () => new TestPocoState())).Value = "test-value";

            var json = JsonObject.Create(botState.Get());

            Assert.Equal("test-value", json["test-name"]["value"].ToString());
        }

        [Fact]
        public async Task BotStateGetCachedState()
        {
            var turnContext = TestUtilities.CreateEmptyContext();
            turnContext.Activity.Conversation = new ConversationAccount { Id = "1234" };

            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>());
            var botState = new TestBotState(storage);
            await botState.LoadAsync(turnContext, false);

            (botState
                .GetValue("test-name", () => new TestPocoState())).Value = "test-value";

            var cache = botState.GetCachedState();

            Assert.NotNull(cache);

            Assert.Same(cache, botState.GetCachedState());
        }

        [Fact]
        public async Task State_ForceIsNoOpWithoutCachedBotState()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await userState.SaveChangesAsync(context, true);
        }

        [Fact]
        public async Task State_ForceCallsSaveWithoutCachedBotStateChanges()
        {
            // Mock a storage provider, which counts writes
            var storeCount = 0;
            var dictionary = new Dictionary<string, object>();
            var mock = new Mock<IStorage>();
            mock.Setup(ms => ms.WriteAsync(It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => storeCount++);
            mock.Setup(ms => ms.ReadAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(result: (IDictionary<string, object>)dictionary));

            // Arrange
            var userState = new UserState(mock.Object);
            var context = TestUtilities.CreateEmptyContext();

            await userState.LoadAsync(context, false);

            // Act
            // Set initial value and save
            userState.SetValue("propertyA", "test");
            await userState.SaveChangesAsync(context);

            // Assert
            Assert.Equal(1, storeCount);

            // Saving without changes and wthout force does NOT call .WriteAsync
            await userState.SaveChangesAsync(context);
            Assert.Equal(1, storeCount);

            // Forcing save without changes DOES call .WriteAsync
            await userState.SaveChangesAsync(context, true);
            Assert.Equal(2, storeCount);
        }

        public class TypedObject
        {
            public string Name { get; set; }
        }

        public class TestBotState : AgentState
        {
            public TestBotState(IStorage storage)
                : base(storage, $"BotState:{typeof(AgentState).Namespace}.{typeof(AgentState).Name}")
            {
            }

            protected override string GetStorageKey(ITurnContext turnContext) => $"botstate/{turnContext.Activity.ChannelId}/{turnContext.Activity.Conversation.Id}/{typeof(AgentState).Namespace}.{typeof(AgentState).Name}";
        }

        public class CustomState : IStoreItem
        {
            public string CustomString { get; set; }

            public string ETag { get; set; }
        }

        public class CustomKeyState : AgentState
        {
            public const string PropertyName = "Microsoft.Bot.Builder.Tests.CustomKeyState";

            public CustomKeyState(IStorage storage)
                : base(storage, PropertyName)
            {
            }

            protected override string GetStorageKey(ITurnContext turnContext) => "CustomKey";
        }
    }
}
