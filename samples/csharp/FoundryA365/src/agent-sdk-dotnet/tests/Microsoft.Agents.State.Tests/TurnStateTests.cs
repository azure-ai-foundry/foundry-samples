// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.State.Tests
{
    public class TurnStateTests
    {
        [Fact]
        public void TurnState_Properties()
        {
            var storage = new MemoryStorage();
            var turnState = new TurnState(new UserState(storage), new ConversationState(storage));

            Assert.IsType<UserState>(turnState.GetScope(UserState.ScopeName));
            Assert.IsType<ConversationState>(turnState.GetScope(ConversationState.ScopeName));
        }

        [Fact]
        public async Task TurnState_LoadAsync()
        {
            var storage = new MemoryStorage();

            var turnContext = TestUtilities.CreateEmptyContext();
            {
                var turnState = new TurnState(new UserState(storage), new ConversationState(storage));
                await turnState.LoadStateAsync(turnContext, cancellationToken: CancellationToken.None, force:false);

                var userCount = turnState.GetValue("user.userCount", () => 0);
                Assert.Equal(0, userCount);
                var convCount = turnState.GetValue("conversation.convCount", () => 0);
                Assert.Equal(0, convCount);

                turnState.SetValue("user.userCount", 10);
                turnState.SetValue("conversation.convCount", 20);

                Assert.Equal(10, turnState.GetValue("user.userCount", () => 0));
                Assert.Equal(20, turnState.GetValue("conversation.convCount", () => 0));

                await turnState.SaveStateAsync(turnContext, cancellationToken: CancellationToken.None, force:false);
            }

            {
                var turnState = new TurnState(new UserState(storage), new ConversationState(storage));

                await turnState.LoadStateAsync(turnContext, cancellationToken: CancellationToken.None);

                var userCount = turnState.GetValue("user.userCount", () => 0);
                Assert.Equal(10, userCount);
                var convCount = turnState.GetValue("conversation.convCount", () => 0);
                Assert.Equal(20, convCount);
            }
        }

        [Fact]
        public void TurnState_TempState()
        {
            var turnState = new TurnState(new TempState());

            // Get should create property
            var count = turnState.Temp.GetValue("count", () => 1);
            Assert.Equal(1, count);

            // Get via path
            count = turnState.GetValue<int>("temp.count");
            Assert.Equal(1, count);

            turnState.Temp.SetValue("count", 2);
            count = turnState.GetValue<int>("temp.count");
            Assert.Equal(2, count);
        }

        /*
        [Fact]
        public async Task TurnState_DottedProperties()
        {
            var test = new
            {
                test = "test",

                options = new
                {
                    Age = 15,
                    FirstName = "joe",
                    LastName = "blow",
                    Bool = false,
                    Nicknames = new List<string>( [ "Tom", "Rex" ] )
                },

                bar = new
                {
                    numIndex = 2,
                    strIndex = "FirstName",
                    objIndex = "options",
                    options = new Options()
                    {
                        Age = 1,
                        FirstName = "joe",
                        LastName = "blow",
                        Bool = false,
                    },
                    numbers = new int[] { 1, 2, 3, 4, 5 }
                },
            };

            var storage = new MemoryStorage();

            var turnContext = TestUtilities.CreateEmptyContext();
            {
                var turnState = new TurnState(new UserState(storage), new ConversationState(storage));
                await turnState.LoadStateAsync(turnContext, false);

                // Add a couple array elements
                turnState.SetValue("conversation.x.a[1]", "yabba");
                turnState.SetValue("conversation.x.a[0]", "dabba");

                Assert.Equal("dabba", turnState.GetValue("conversation.x.a[0]", () => string.Empty));
                Assert.Equal("yabba", turnState.GetValue("conversation.x.a[1]", () => string.Empty));

                // Verify array
                var array = turnState.GetValue("conversation.x.a", () => Array.Empty<string>());
                Assert.IsAssignableFrom<Array>(array);
                Assert.Equal(2, array.Length);
                Assert.Equal("dabba", array[0]);
                Assert.Equal("yabba", array[1]);

                // Anonymous type access
                turnState.SetValue("user.test", test);

                Assert.Equal("FirstName", turnState.GetValue("user.test.bar.strIndex", () => string.Empty));
                Assert.Equal("Rex", turnState.GetValue("user.test.options.Nicknames[1]", () => string.Empty));
                Assert.Equal(2, turnState.GetValue("user.test.bar.numbers[1]", () => -1));

                // Anonymous types are read only
                Assert.Throws<ArgumentException>(() => turnState.SetValue("user.test.bar.strIndex", "NewFirstName"));

                // Don't support growing native arrays yet
                Assert.Throws<ArgumentException>(() => turnState.SetValue("user.test.bar.numbers[10]", 10));

                // But it can grow lists
                turnState.SetValue("user.test.options.Nicknames[5]", "John");
                Assert.Equal("John", turnState.GetValue("user.test.options.Nicknames[5]", () => string.Empty));

                // Can set poco
                var poco = new TestPocoState()
                {
                    Value = "firstValue"
                };
                turnState.SetValue("user.poco", poco);
                Assert.Equal("firstValue", turnState.GetValue("user.poco.Value", () => string.Empty));

                // Get poco object
                var pocoOut = turnState.GetValue("user.poco", () => new TestPocoState());
                Assert.Equal("firstValue", pocoOut.Value);

                // Can set poco field
                turnState.SetValue("user.poco.Value", "secondValue");
                Assert.Equal("secondValue", turnState.GetValue("user.poco.Value", () => string.Empty));

                // List
                turnState.SetValue("conversation.chatHistory", new List<string>());
                var chatHistory = turnState.GetValue("conversation.chatHistory", () => new List<string>());
                chatHistory.Add("Hello");
                chatHistory.Add("Howdy");

                Assert.Equal("Hello", turnState.GetValue("conversation.chatHistory[0]", () => string.Empty));
                Assert.Equal("Howdy", turnState.GetValue("conversation.chatHistory[1]", () => string.Empty));
            }
        }
        */
         
        [Fact]
        public async Task TurnState_ReturnsDefaultForNullValueType()
        {
            var storage = new MemoryStorage();
            var turnContext = TestUtilities.CreateEmptyContext();
            var turnState = new TurnState(new UserState(storage), new ConversationState(storage));
            await turnState.LoadStateAsync(turnContext, cancellationToken: CancellationToken.None, force:false);

            var userObject = turnState.GetValue<string>("user.userStateObject");
            Assert.Null(userObject);

            // Ensure we also get null on second attempt
            userObject = turnState.GetValue<string>("user.userStateObject");
            Assert.Null(userObject);

            var convObject = turnState.GetValue<string>("conversation.convStateObject");
            Assert.Null(convObject);

            // Ensure we also get null on second attempt
            convObject = turnState.GetValue<string>("conversation.convStateObject");
            Assert.Null(convObject);
        }

        [Fact]
        public async Task TurnState_SaveAsync()
        {
            var storage = new MemoryStorage();

            // setup userstate
            var userState = new UserState(storage);

            // setup convState
            var convState = new ConversationState(storage);

            var turnState = new TurnState(userState, convState);

            var context = TestUtilities.CreateEmptyContext();
            await turnState.LoadStateAsync(context, cancellationToken: CancellationToken.None);

            var userCount = userState.GetValue("userCount", () => 0);
            Assert.Equal(0, userCount);
            var convCount = convState.GetValue("convCount", () => 0);
            Assert.Equal(0, convCount);

            userState.SetValue("userCount", 10);
            convState.SetValue("convCount", 20);

            await turnState.SaveStateAsync(context, cancellationToken: CancellationToken.None);

            userCount = userState.GetValue("userCount", () => 0);
            Assert.Equal(10, userCount);

            convCount = convState.GetValue("convCount", () => 0);
            Assert.Equal(20, convCount);
        }

        internal class SomeComplexType
        {
            public string PropA { get; set; }

            public int PropB { get; set; }
        }
    }
}
