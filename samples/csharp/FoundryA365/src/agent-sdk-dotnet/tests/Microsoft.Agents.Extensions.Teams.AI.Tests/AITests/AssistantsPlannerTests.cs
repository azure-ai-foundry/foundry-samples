// using Microsoft.Agents.Builder;
// using Microsoft.Agents.Extensions.Teams.AI;
// using Microsoft.Agents.Extensions.Teams.AI.Planners.Experimental;
// using Microsoft.Agents.Extensions.Teams.AI.Exceptions;
// using Record = Microsoft.Agents.Extensions.Teams.AI.State.Record;
// using Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils;
// using Moq;
// using System.Reflection;
// using Microsoft.Agents.Extensions.Teams.AI.Planners;
// using OpenAI.Assistants;
// using Microsoft.Agents.Core.Models;
// using Microsoft.Agents.Storage;
// using Microsoft.Agents.Builder.State;

// namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests
// {
//     public class AssistantsPlannerTests
//     {
//         [Fact]
//         public async Task Test_BeginTaskAsync_Assistant_Single_Reply()
//         {
//             // Arrange
//             var testClient = new TestAssistantsOpenAIClient();
//             var planner = new AssistantsPlanner<ITurnState>(new("test-key", "test-assistant-id")
//             {
//                 PollingInterval = TimeSpan.FromMilliseconds(100)
//             });
//             planner.GetType().GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(planner, testClient);

//             var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
//             var storage = new MemoryStorage();
//             var turnState = new AssistantsState(storage);
//             await turnState.LoadStateAsync(turnContext);
            
//             turnState.Temp!.SetValue("input", "hello");

//             var aiOptions = new AIOptions(planner);
//             var ai = new AISystem(aiOptions);

//             testClient.RemainingRunStatus.Enqueue("completed");
//             testClient.RemainingMessages.Enqueue("welcome");

//             // Act
//             var plan = await planner.BeginTaskAsync(turnContext, turnState, ai, CancellationToken.None);

//             // Assert
//             Assert.NotNull(plan);
//             Assert.NotNull(plan.Commands);
//             Assert.Single(plan.Commands);
//             Assert.Equal(AIConstants.SayCommand, plan.Commands[0].Type);
//             Assert.Equal("welcome", ((PredictedSayCommand)plan.Commands[0]).Response.Content);
//         }

//         [Fact]
//         public async Task Test_BeginTaskAsync_Assistant_WaitForCurrentRun()
//         {
//             // Arrange
//             var testClient = new TestAssistantsOpenAIClient();
//             var planner = new AssistantsPlanner<AssistantsState>(new("test-key", "test-assistant-id")
//             {
//                 PollingInterval = TimeSpan.FromMilliseconds(100)
//             });
//             planner.GetType().GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(planner, testClient);

//             var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
//             var storage = new MemoryStorage();
//             var turnState = new AssistantsState(storage);
//             await turnState.LoadStateAsync(turnContext);
//             turnState.Temp!.SetValue("input", "hello");

//             var aiOptions = new AIOptions(planner);
//             var ai = new AISystem(aiOptions);

//             testClient.RemainingRunStatus.Enqueue("in_progress");
//             testClient.RemainingRunStatus.Enqueue("completed");
//             testClient.RemainingMessages.Enqueue("welcome");

//             // Act
//             var plan = await planner.BeginTaskAsync(turnContext, turnState, ai, CancellationToken.None);

//             // Assert
//             Assert.NotNull(plan);
//             Assert.NotNull(plan.Commands);
//             Assert.Single(plan.Commands);
//             Assert.Equal(AIConstants.SayCommand, plan.Commands[0].Type);
//             Assert.Equal("welcome", ((PredictedSayCommand)plan.Commands[0]).Response.Content);
//         }

//         [Fact]
//         public async Task Test_BeginTaskAsync_Assistant_WaitForPreviousRun()
//         {
//             // Arrange
//             var testClient = new TestAssistantsOpenAIClient();
//             var planner = new AssistantsPlanner<AssistantsState>(new("test-key", "test-assistant-id")
//             {
//                 PollingInterval = TimeSpan.FromMilliseconds(100)
//             });
//             planner.GetType().GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(planner, testClient);

//             var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
//             var storage = new MemoryStorage();
//             var turnState = new AssistantsState(storage);
//             await turnState.LoadStateAsync(turnContext);
//             turnState.Temp!.SetValue("input", "hello");

//             var aiOptions = new AIOptions(planner);
//             var ai = new AISystem(aiOptions);

//             testClient.RemainingRunStatus.Enqueue("failed");
//             testClient.RemainingRunStatus.Enqueue("completed");
//             testClient.RemainingMessages.Enqueue("welcome");

//             AssistantThread thread = await testClient.CreateThreadAsync(new(), CancellationToken.None);
//             await testClient.CreateRunAsync(thread.Id, "", OpenAIModelFactory.CreateRunOptions(), CancellationToken.None);
//             turnState.ThreadId = thread.Id;

//             // Act
//             var plan = await planner.BeginTaskAsync(turnContext, turnState, ai, CancellationToken.None);

//             // Assert
//             Assert.NotNull(plan);
//             Assert.NotNull(plan.Commands);
//             Assert.Single(plan.Commands);
//             Assert.Equal(AIConstants.SayCommand, plan.Commands[0].Type);
//             Assert.Equal("welcome", ((PredictedSayCommand)plan.Commands[0]).Response.Content);
//         }

//         [Fact]
//         public async Task Test_BeginTaskAsync_Assistant_RunCancelled()
//         {
//             // Arrange
//             var testClient = new TestAssistantsOpenAIClient();
//             var planner = new AssistantsPlanner<AssistantsState>(new("test-key", "test-assistant-id")
//             {
//                 PollingInterval = TimeSpan.FromMilliseconds(100)
//             });
//             planner.GetType().GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(planner, testClient);

//             var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
//             var storage = new MemoryStorage();
//             var turnState = new AssistantsState(storage);
//             await turnState.LoadStateAsync(turnContext);
//             turnState.Temp!.SetValue("input", "hello");

//             var aiOptions = new AIOptions(planner);
//             var ai = new AISystem(aiOptions);

//             testClient.RemainingRunStatus.Enqueue("cancelled");
//             testClient.RemainingMessages.Enqueue("welcome");

//             // Act
//             var plan = await planner.BeginTaskAsync(turnContext, turnState, ai, CancellationToken.None);

//             // Assert
//             Assert.NotNull(plan);
//             Assert.NotNull(plan.Commands);
//             Assert.Empty(plan.Commands);
//         }

//         [Fact]
//         public async Task Test_BeginTaskAsync_Assistant_RunExpired()
//         {
//             // Arrange
//             var testClient = new TestAssistantsOpenAIClient();
//             var planner = new AssistantsPlanner<AssistantsState>(new("test-key", "test-assistant-id")
//             {
//                 PollingInterval = TimeSpan.FromMilliseconds(100)
//             });
//             planner.GetType().GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(planner, testClient);
//             var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
//             var storage = new MemoryStorage();
//             var turnState = new AssistantsState(storage);
//             await turnState.LoadStateAsync(turnContext);
//             turnState.Temp!.SetValue("input", "hello");

//             var aiOptions = new AIOptions(planner);
//             var ai = new AISystem(aiOptions);

//             testClient.RemainingRunStatus.Enqueue("expired");
//             testClient.RemainingMessages.Enqueue("welcome");

//             // Act
//             var plan = await planner.BeginTaskAsync(turnContext, turnState, ai, CancellationToken.None);

//             // Assert
//             Assert.NotNull(plan);
//             Assert.NotNull(plan.Commands);
//             Assert.Single(plan.Commands);
//             Assert.Equal(AIConstants.DoCommand, plan.Commands[0].Type);
//             Assert.Equal(AIConstants.TooManyStepsActionName, ((PredictedDoCommand)plan.Commands[0]).Action);
//         }

//         [Fact]
//         public async Task Test_BeginTaskAsync_Assistant_RunFailed()
//         {
//             // Arrange
//             var testClient = new TestAssistantsOpenAIClient();
//             var planner = new AssistantsPlanner<AssistantsState>(new("test-key", "test-assistant-id")
//             {
//                 PollingInterval = TimeSpan.FromMilliseconds(100)
//             });
//             planner.GetType().GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(planner, testClient);
//             var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
//             var storage = new MemoryStorage();
//             var turnState = new AssistantsState(storage);
//             await turnState.LoadStateAsync(turnContext);
//             turnState.Temp!.SetValue("input", "hello");

//             var aiOptions = new AIOptions(planner);
//             var ai = new AISystem(aiOptions);

//             testClient.RemainingRunStatus.Enqueue("failed");
//             testClient.RemainingMessages.Enqueue("welcome");

//             // Act
//             var exception = await Assert.ThrowsAsync<TeamsAIException>(() => planner.BeginTaskAsync(turnContext, turnState, ai, CancellationToken.None));

//             // Assert
//             Assert.NotNull(exception);
//             Assert.NotNull(exception.Message);
//             Assert.True(exception.Message.IndexOf("Run failed") >= 0);
//         }

//         [Fact]
//         public async Task Test_ContinueTaskAsync_Assistant_RequiresAction()
//         {
//             // Arrange
//             var testClient = new TestAssistantsOpenAIClient();
//             var planner = new AssistantsPlanner<AssistantsState>(new("test-key", "test-assistant-id")
//             {
//                 PollingInterval = TimeSpan.FromMilliseconds(100)
//             });
//             planner.GetType().GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(planner, testClient);
//             var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
//             var storage = new MemoryStorage();
//             var turnState = new AssistantsState(storage);
//             await turnState.LoadStateAsync(turnContext);
//             turnState.Temp!.SetValue("actionOutputs", new Dictionary<string, string>());
//             turnState.Temp!.SetValue("input", "hello");

//             var aiOptions = new AIOptions(planner);
//             var ai = new AISystem(aiOptions);

//             var requiredAction = OpenAIModelFactory.CreateRequiredAction("test-tool-id", "test-action", "{}");

//             testClient.RemainingActions.Enqueue(requiredAction);
//             testClient.RemainingRunStatus.Enqueue("requires_action");
//             testClient.RemainingRunStatus.Enqueue("in_progress");
//             testClient.RemainingRunStatus.Enqueue("completed");
//             testClient.RemainingMessages.Enqueue("welcome");

//             // Act
//             var plan1 = await planner.ContinueTaskAsync(turnContext, turnState, ai, CancellationToken.None);
//             var actionOutputs = turnState.Temp.GetValue<Dictionary<string, string>>("actionOutputs");
//             actionOutputs["test-action"] = "test-output";
//             turnState.Temp.SetValue("actionOutputs", actionOutputs);
            
//             var plan2 = await planner.ContinueTaskAsync(turnContext, turnState, ai, CancellationToken.None);

//             // Assert
//             Assert.NotNull(plan1);
//             Assert.NotNull(plan1.Commands);
//             Assert.Single(plan1.Commands);
//             Assert.Equal(AIConstants.DoCommand, plan1.Commands[0].Type);
//             Assert.Equal("test-action", ((PredictedDoCommand)plan1.Commands[0]).Action);
//             Assert.NotNull(plan2);
//             Assert.NotNull(plan2.Commands);
//             Assert.Single(plan2.Commands);
//             Assert.Equal(AIConstants.SayCommand, plan2.Commands[0].Type);
//             Assert.Equal("welcome", ((PredictedSayCommand)plan2.Commands[0]).Response.Content);
//             Assert.Single(turnState.SubmitToolMap);
//             Assert.Equal("test-action", turnState.SubmitToolMap.First().Key);
//             Assert.Equal("test-tool-id", turnState.SubmitToolMap.First().Value);
//         }

//         [Fact]
//         public async Task Test_ContinueTaskAsync_Assistant_IgnoreRedundantAction()
//         {
//             // Arrange
//             var testClient = new TestAssistantsOpenAIClient();
//             var planner = new AssistantsPlanner<AssistantsState>(new("test-key", "test-assistant-id")
//             {
//                 PollingInterval = TimeSpan.FromMilliseconds(100)
//             });
//             planner.GetType().GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(planner, testClient);
//             var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
//             var storage = new MemoryStorage();
//             var turnState = new AssistantsState(storage);
//             await turnState.LoadStateAsync(turnContext);
//             turnState.Temp!.SetValue("actionOutputs", new Dictionary<string, string>());

//             turnState.Temp!.SetValue("input", "hello");

//             var actionOutputs = turnState.Temp.GetValue<Dictionary<string, string>>("actionOutputs");
//             actionOutputs["other-action"] = "should not be used";
//             turnState.Temp.SetValue("actionOutputs", actionOutputs);


//             var aiOptions = new AIOptions(planner);
//             var ai = new AISystem(aiOptions);

//             var requiredAction = OpenAIModelFactory.CreateRequiredAction("test-tool-id", "test-action", "{}");

//             testClient.RemainingActions.Enqueue(requiredAction);
//             testClient.RemainingRunStatus.Enqueue("requires_action");
//             testClient.RemainingRunStatus.Enqueue("in_progress");
//             testClient.RemainingRunStatus.Enqueue("completed");
//             testClient.RemainingMessages.Enqueue("welcome");

//             // Act
//             var plan1 = await planner.ContinueTaskAsync(turnContext, turnState, ai, CancellationToken.None);

//             var newActionOutputs = turnState.Temp.GetValue<Dictionary<string, string>>("actionOutputs");
//             actionOutputs["test-action"] = "test-output";
//             turnState.Temp.SetValue("actionOutputs", actionOutputs);

//             var plan2 = await planner.ContinueTaskAsync(turnContext, turnState, ai, CancellationToken.None);

//             // Assert
//             Assert.NotNull(plan1);
//             Assert.NotNull(plan1.Commands);
//             Assert.Single(plan1.Commands);
//             Assert.Equal(AIConstants.DoCommand, plan1.Commands[0].Type);
//             Assert.Equal("test-action", ((PredictedDoCommand)plan1.Commands[0]).Action);
//             Assert.NotNull(plan2);
//             Assert.NotNull(plan2.Commands);
//             Assert.Single(plan2.Commands);
//             Assert.Equal(AIConstants.SayCommand, plan2.Commands[0].Type);
//             Assert.Equal("welcome", ((PredictedSayCommand)plan2.Commands[0]).Response.Content);
//             Assert.Single(turnState.SubmitToolMap);
//             Assert.Equal("test-action", turnState.SubmitToolMap.First().Key);
//             Assert.Equal("test-tool-id", turnState.SubmitToolMap.First().Value);
//         }


//         [Fact]
//         public async Task Test_ContinueTaskAsync_Assistant_MultipleMessages()
//         {
//             // Arrange
//             var testClient = new TestAssistantsOpenAIClient();
//             var planner = new AssistantsPlanner<AssistantsState>(new("test-key", "test-assistant-id")
//             {
//                 PollingInterval = TimeSpan.FromMilliseconds(100)
//             });
//             planner.GetType().GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(planner, testClient);
//             var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
//             var storage = new MemoryStorage();
//             var turnState = new AssistantsState(storage);
//             await turnState.LoadStateAsync(turnContext);
//             turnState.Temp!.SetValue("actionOutputs", new Dictionary<string, string>());
//             turnState.Temp!.SetValue("input", "hello");

//             var aiOptions = new AIOptions(planner);
//             var ai = new AISystem(aiOptions);

//             testClient.RemainingRunStatus.Enqueue("completed");
//             testClient.RemainingMessages.Enqueue("welcome");
//             testClient.RemainingMessages.Enqueue("message 1");
//             testClient.RemainingMessages.Enqueue("message 2");

//             // Act
//             var plan = await planner.ContinueTaskAsync(turnContext, turnState, ai, CancellationToken.None);

//             // Assert
//             Assert.NotNull(plan);
//             Assert.NotNull(plan.Commands);
//             Assert.Equal(3, plan.Commands.Count);
//             Assert.Equal(AIConstants.SayCommand, plan.Commands[0].Type);
//             Assert.Equal("welcome", ((PredictedSayCommand)plan.Commands[0]).Response.Content);
//             Assert.Equal("message 1", ((PredictedSayCommand)plan.Commands[1]).Response.Content);
//             Assert.Equal("message 2", ((PredictedSayCommand)plan.Commands[2]).Response.Content);
//         }

//         private static async Task<AssistantsState> _CreateAssistantsState()
//         {
//             var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
//             var storage = new MemoryStorage();
//             var turnState = new AssistantsState(storage);
//             await turnState.LoadStateAsync(turnContext);
//             return turnState;
//         }
//     }
// }
