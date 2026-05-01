using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Extensions.Teams.AI;
using Microsoft.Agents.Extensions.Teams.AI.Action;
using Microsoft.Agents.Extensions.Teams.AI.Moderator;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils;
using Moq;
using System.Net.Http.Headers;
using System.Reflection;
using Plan = Microsoft.Agents.Extensions.Teams.AI.Planners.Plan;
using Record = Microsoft.Agents.Extensions.Teams.AI.State.Record;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests
{
    public class AITests
    {
        [Fact]
        public void Test_RegisterAction_RegisterSameActionTwice()
        {
            // Arrange
            var planner = new TestPlanner();
            var moderator = new TestModerator();
            var options = new AIOptions(planner)
            {
                Moderator = moderator
            };
            var ai = new AISystem(options);
            var handler = new TestActionHandler();

            // Act
            ai.RegisterAction("test-action", handler);
            var containsAction = ai.ContainsAction("test-action");
            var exception = Assert.Throws<InvalidOperationException>(() => ai.RegisterAction("test-action", handler));

            // Assert
            Assert.True(containsAction);
            Assert.NotNull(exception);
            Assert.Equal("Attempting to register an already existing action `test-action` that does not allow overrides.", exception.Message);
        }

        [Fact]
        public async Task Test_RegisterAction_OverrideDefaultAction()
        {
            // Arrange
            var planner = new TestPlanner();
            var moderator = new TestModerator();
            var options = new AIOptions(planner)
            {
                Moderator = moderator
            };
            var ai = new AISystem(options);
            var handler = new TestActionHandler();
            var turnContextMock = new Mock<ITurnContext>();
            var turnState = new TurnState();

            // Act
            ai.RegisterAction(AIConstants.UnknownActionName, handler);
            FieldInfo actionsField = typeof(AISystem).GetField("_actions", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance)!;
            IActionCollection<ITurnState> actions = (IActionCollection<ITurnState>)actionsField!.GetValue(ai)!;
            var result = await actions[AIConstants.UnknownActionName].Handler.PerformActionAsync(turnContextMock.Object, turnState, null, null);
            var exception = Assert.Throws<InvalidOperationException>(() => ai.RegisterAction(AIConstants.UnknownActionName, handler));

            // Assert
            Assert.Equal("test-result", result);
            Assert.NotNull(exception);
            Assert.Equal($"Attempting to register an already existing action `{AIConstants.UnknownActionName}` that does not allow overrides.", exception.Message);
        }

        [Fact]
        public async Task Test_RunAsync()
        {
            // Arrange
            var planner = new TurnStatePlanner<ITurnState>();
            var moderator = new TurnStateModerator<ITurnState>();
            var options = new AIOptions(planner)
            {
                Moderator = moderator
            };
            var ai = new AISystem(options);

            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var turnState = new TurnState(storage);
            await turnState.LoadStateAsync(turnContext);

            var actions = new TestActions();
            ai.ImportActions(actions);

            // Act
            var result = await ai.RunAsync(turnContext, turnState);

            // Assert
            Assert.True(result);
            Assert.Equal(new string[] { "BeginTaskAsync" }, planner.Record.ToArray());
            Assert.Equal(new string[] { "ReviewInputAsync", "ReviewOutputAsync" }, moderator.Record.ToArray());
            Assert.Equal(new string[] { "Test-DO" }, actions.DoActionRecord.ToArray());
            Assert.Equal(new string[] { "Test-SAY" }, actions.SayActionRecord.ToArray());
        }

        [Fact]
        public async Task Test_RunAsync_ExceedStepLimit()
        {
            var planner = new TurnStatePlanner<ITurnState>();
            var moderator = new TurnStateModerator<ITurnState>();
            var options = new AIOptions(planner)
            {
                Moderator = moderator
            };
            var ai = new AISystem(options);
            var botAdapterStub = Mock.Of<IChannelAdapter>();
            var turnContextMock = new TurnContext(botAdapterStub,
                new Activity
                {
                    Text = "user message",
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From = new() { Id = "fromId" },
                    ChannelId = "channelId"
                });
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContextMock);
            var actions = new TestActions();
            ai.ImportActions(actions);
            var actionHandler = new TurnStateActionHandler();
            ai.RegisterAction(AIConstants.TooManyStepsActionName, actionHandler);

            // Act
            var result = await ai.RunAsync(turnContextMock, turnState, stepCount: 30);

            // Assert
            Assert.False(result);
            Assert.Equal(new string[] { "ContinueTaskAsync" }, planner.Record.ToArray());
            Assert.Equal(new string[] { "ReviewOutputAsync" }, moderator.Record.ToArray());
            Assert.Equal(new string[] { }, actions.DoActionRecord.ToArray());
            Assert.Equal(new string[] { }, actions.SayActionRecord.ToArray());
        }

        [Fact]
        public async Task Test_RunAsync_ExceedTimeLimit()
        {
            // Arrange
            var planner = new TurnStatePlanner<ITurnState>();
            var moderator = new TurnStateModerator<ITurnState>();
            var options = new AIOptions(planner)
            {
                Moderator = moderator,
                MaxTime = TimeSpan.Zero
            };
            var ai = new AISystem(options);
            var botAdapterStub = Mock.Of<IChannelAdapter>();
            var turnContextMock = new TurnContext(botAdapterStub,
                new Activity
                {
                    Text = "user message",
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From = new() { Id = "fromId" },
                    ChannelId = "channelId"
                });
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContextMock);
            var actions = new TestActions();
            ai.ImportActions(actions);
            ai.RegisterAction(AIConstants.TooManyStepsActionName, new TurnStateActionHandler());

            // Act
            var result = await ai.RunAsync(turnContextMock, turnState);

            // Assert
            Assert.False(result);
            Assert.Equal(new string[] { "BeginTaskAsync" }, planner.Record.ToArray());
            Assert.Equal(new string[] { "ReviewInputAsync", "ReviewOutputAsync" }, moderator.Record.ToArray());
            Assert.Equal(new string[] { }, actions.DoActionRecord.ToArray());
            Assert.Equal(new string[] { }, actions.SayActionRecord.ToArray());
        }

        /// <summary>
        /// Override default DO and SAY actions for test
        /// </summary>
        private sealed class TestActions
        {
            public IList<string> DoActionRecord { get; } = new List<string>();

            public IList<string> SayActionRecord { get; } = new List<string>();

            [Action("Test-DO")]
            public string DoCommand([ActionName] string action)
            {
                DoActionRecord.Add(action);
                return string.Empty;
            }

            [Action(AIConstants.SayCommandActionName)]
            public string SayCommand([ActionParameters] PredictedSayCommand command)
            {
                SayActionRecord.Add(command.Response.GetContent<string>());
                return string.Empty;
            }
        }
    }

    internal sealed class TurnStateActionHandler : IActionHandler<ITurnState>
    {
        public string? ActionName { get; set; }
        public Task<string> PerformActionAsync(ITurnContext turnContext, ITurnState turnState, object? entities = null, string? action = null, CancellationToken cancellationToken = default)
        {
            ActionName = action;
            return Task.FromResult("test-result");
        }
    }

    internal sealed class TurnStateModerator<TState> : IModerator<TState> where TState : ITurnState
    {
        public IList<string> Record { get; } = new List<string>();

        public Task<Plan?> ReviewInputAsync(ITurnContext turnContext, TState turnState, CancellationToken cancellationToken = default)
        {
            Record.Add(MethodBase.GetCurrentMethod()!.Name);
            return Task.FromResult<Plan?>(null);
        }

        public Task<Plan> ReviewOutputAsync(ITurnContext turnContext, TState turnState, Plan plan, CancellationToken cancellationToken = default)
        {
            Record.Add(MethodBase.GetCurrentMethod()!.Name);
            return Task.FromResult(plan);
        }
    }

    internal sealed class TurnStatePlanner<T> : IPlanner<T> where T : ITurnState
    {
        public IList<string> Record { get; } = new List<string>();

        public Plan BeginPlan { get; set; } = new Plan
        {
            Commands = new List<IPredictedCommand>
            {
                new PredictedDoCommand("Test-DO"),
                new PredictedSayCommand("Test-SAY")
            }
        };

        public Plan ContinuePlan { get; set; } = new Plan
        {
            Commands = new List<IPredictedCommand>
            {
                new PredictedDoCommand("Test-DO"),
                new PredictedSayCommand("Test-SAY")
            }
        };

        public Task<Plan> BeginTaskAsync(ITurnContext turnContext, T turnState, AISystem ai, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod()!.Name);
            return Task.FromResult(BeginPlan);
        }

        public Task<Plan> ContinueTaskAsync(ITurnContext turnContext, T turnState, AISystem ai, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod()!.Name);
            return Task.FromResult(ContinuePlan);
        }
    }
}
