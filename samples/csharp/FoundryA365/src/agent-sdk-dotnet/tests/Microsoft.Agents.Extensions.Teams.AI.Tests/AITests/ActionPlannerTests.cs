using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Extensions.Teams.AI;
using Microsoft.Agents.Extensions.Teams.AI.Augmentations;
using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.Validators;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils;
using Moq;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests
{
    public class ActionPlannerTests
    {
        [Fact]
        public async Task Test_CompletePromptAsync_HasPrompt()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var responseMock = new Mock<PromptResponse>();
            modelMock.Setup(model => model.CompletePromptAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<TurnState>(),
                It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptTemplate>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(responseMock.Object);
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var prompts = new PromptManager();
            prompts.AddPrompt("prompt", promptTemplate);
            var options = new ActionPlannerOptions<TurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(new Mock<PromptTemplate>().Object)
            );

            var context = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var memory = new TurnState(storage);
            await memory.LoadStateAsync(context);

            var planner = new ActionPlanner<TurnState>(options, new TestLoggerFactory());

            // Act
            var result = await planner.CompletePromptAsync(context, memory, promptTemplate, null);

            // Assert
            Assert.Equal(responseMock.Object, result);
        }

        [Fact]
        public async Task Test_CompletePromptAsync_DoesNotHavePrompt()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var responseMock = new Mock<PromptResponse>();
            modelMock.Setup(model => model.CompletePromptAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<TurnState>(),
                It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptTemplate>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(responseMock.Object);
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var prompts = new PromptManager();
            var options = new ActionPlannerOptions<TurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(new Mock<PromptTemplate>().Object)
            );
            var context = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var memory = new TurnState(storage);
            await memory.LoadStateAsync(context);
            var planner = new ActionPlanner<TurnState>(options, new TestLoggerFactory());

            // Act
            var result = await planner.CompletePromptAsync(context, memory, promptTemplate, null);

            // Assert
            Assert.True(prompts.HasPrompt("prompt"));
            Assert.Equal(responseMock.Object, result);
        }

        [Fact]
        public async Task Test_ContinueTaskAsync_PromptResponseStatusError()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var response = new PromptResponse()
            {
                Status = PromptResponseStatus.Error,
                Error = new("failed")
            };
            modelMock.Setup(model => model.CompletePromptAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptTemplate>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var prompts = new PromptManager();
            prompts.AddPrompt("prompt", promptTemplate);
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );
            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var state = new TurnState(storage);
            await state.LoadStateAsync(turnContext);
            
            state.Temp.SetValue("input", "test");
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());
            var ai = new AISystem(new(planner));

            // Act
            var exception = await Assert.ThrowsAsync<Exception>(async () => await planner.ContinueTaskAsync(turnContext, state, ai));

            // Assert
            Assert.Equal("failed", exception.Message);
        }

        [Fact]
        public async Task Test_ContinueTaskAsync_PromptResponseStatusError_ErrorNull()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var response = new PromptResponse()
            {
                Status = PromptResponseStatus.Error
            };
            modelMock.Setup(model => model.CompletePromptAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptTemplate>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var prompts = new PromptManager();
            prompts.AddPrompt("prompt", promptTemplate);
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );
            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var state = new TurnState(storage);
            await state.LoadStateAsync(turnContext);

            state.Temp.SetValue("input", "test");
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());
            var ai = new AISystem(new(planner));

            // Act
            var exception = await Assert.ThrowsAsync<Exception>(async () => await planner.ContinueTaskAsync(turnContext, state, ai));

            // Assert
            Assert.Equal("[Action Planner]: an error has occurred", exception.Message);
        }

        [Fact]
        public async Task Test_ContinueTaskAsync_PlanNull()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var response = new PromptResponse()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.System),
            };
            modelMock.Setup(model => model.CompletePromptAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptTemplate>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var augmentationMock = new Mock<IAugmentation>();
            Plan? plan = null;
            augmentationMock.Setup(augmentation => augmentation.CreatePlanFromResponseAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<PromptResponse>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(plan);
            augmentationMock.Setup(augmentation => augmentation.ValidateResponseAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptResponse>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new Validation { Valid = true });
            promptTemplate.Augmentation = augmentationMock.Object;
            var prompts = new PromptManager();
            prompts.AddPrompt("prompt", promptTemplate);
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );

            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var state = new TurnState(storage);
            await state.LoadStateAsync(turnContext);

            state.Temp.SetValue("input", "test");
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());
            var ai = new AISystem(new(planner));

            // Act
            var exception = await Assert.ThrowsAsync<Exception>(async () => await planner.ContinueTaskAsync(turnContext, state, ai));

            // Assert
            Assert.Equal("[Action Planner]: failed to create plan", exception.Message);
        }

        [Fact]
        public async Task Test_ContinueTaskAsync()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var response = new PromptResponse()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.System),
            };
            modelMock.Setup(model => model.CompletePromptAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptTemplate>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var augmentationMock = new Mock<IAugmentation>();
            var planMock = new Mock<Plan>();
            augmentationMock.Setup(augmentation => augmentation.CreatePlanFromResponseAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<PromptResponse>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(planMock.Object);
            augmentationMock.Setup(augmentation => augmentation.ValidateResponseAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptResponse>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new Validation { Valid = true });
            promptTemplate.Augmentation = augmentationMock.Object;
            var prompts = new PromptManager();
            prompts.AddPrompt("prompt", promptTemplate);
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );

            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var state = new TurnState(storage);
            await state.LoadStateAsync(turnContext);

            state.Temp.SetValue("input", "test");
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());
            var ai = new AISystem(new(planner));

            // Act
            var result = await planner.ContinueTaskAsync(turnContext, state, ai);

            // Assert
            Assert.Equal(planMock.Object, result);
        }

        [Fact]
        public async Task Test_ContinueTaskAsync_Streaming()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var response = new PromptResponse()
            {
                Status = PromptResponseStatus.Success,
            };
            modelMock.Setup(model => model.CompletePromptAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptTemplate>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var augmentationMock = new Mock<IAugmentation>();
            var planMock = new Plan();
            augmentationMock.Setup(augmentation => augmentation.CreatePlanFromResponseAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<PromptResponse>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(planMock);
            augmentationMock.Setup(augmentation => augmentation.ValidateResponseAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptResponse>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new Validation { Valid = true });
            promptTemplate.Augmentation = augmentationMock.Object;
            var prompts = new PromptManager();
            prompts.AddPrompt("prompt", promptTemplate);
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );

            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var state = new TurnState(storage);
            await state.LoadStateAsync(turnContext);
            
            state.Temp.SetValue("input", "test");
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());
            var ai = new AISystem(new(planner) { EnableFeedbackLoop = true });

            // Act
            var result = await planner.ContinueTaskAsync(turnContext, state, ai);

            // Assert
            Assert.Equal(planMock.Type, result.Type);
            Assert.Equal(planMock.Commands, result.Commands);
        }


        [Fact]
        public async Task Test_BeginTaskAsync_PromptResponseStatusError()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var response = new PromptResponse()
            {
                Status = PromptResponseStatus.Error,
                Error = new("failed")
            };
            modelMock.Setup(model => model.CompletePromptAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptTemplate>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var prompts = new PromptManager();
            prompts.AddPrompt("prompt", promptTemplate);
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );
            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var state = new TurnState(storage);
            await state.LoadStateAsync(turnContext);
            
            state.Temp.SetValue("input", "test");
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());
            var ai = new AISystem(new(planner));

            // Act
            var exception = await Assert.ThrowsAsync<Exception>(async () => await planner.BeginTaskAsync(turnContext, state, ai));

            // Assert
            Assert.Equal("failed", exception.Message);
        }

        [Fact]
        public async Task Test_BeginTaskAsync_PromptResponseStatusError_ErrorNull()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var response = new PromptResponse()
            {
                Status = PromptResponseStatus.Error
            };
            modelMock.Setup(model => model.CompletePromptAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptTemplate>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var prompts = new PromptManager();
            prompts.AddPrompt("prompt", promptTemplate);
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );
            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var state = new TurnState(storage);
            await state.LoadStateAsync(turnContext);

            state.Temp.SetValue("input", "test");
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());
            var ai = new AISystem(new(planner));

            // Act
            var exception = await Assert.ThrowsAsync<Exception>(async () => await planner.BeginTaskAsync(turnContext, state, ai));

            // Assert
            Assert.Equal("[Action Planner]: an error has occurred", exception.Message);
        }

        [Fact]
        public async Task Test_BeginTaskAsync_PlanNull()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var response = new PromptResponse()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.System),
            };
            modelMock.Setup(model => model.CompletePromptAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptTemplate>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var augmentationMock = new Mock<IAugmentation>();
            Plan? plan = null;
            augmentationMock.Setup(augmentation => augmentation.CreatePlanFromResponseAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<PromptResponse>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(plan);
            augmentationMock.Setup(augmentation => augmentation.ValidateResponseAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptResponse>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new Validation { Valid = true });
            promptTemplate.Augmentation = augmentationMock.Object;
            var prompts = new PromptManager();
            prompts.AddPrompt("prompt", promptTemplate);
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );
            
            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var state = new TurnState(storage);
            await state.LoadStateAsync(turnContext);

            state.Temp.SetValue("input", "test");
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());
            var ai = new AISystem(new(planner));

            // Act
            var exception = await Assert.ThrowsAsync<Exception>(async () => await planner.BeginTaskAsync(turnContext, state, ai));

            // Assert
            Assert.Equal("[Action Planner]: failed to create plan", exception.Message);
        }

        [Fact]
        public async Task Test_BeginTaskAsync()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var response = new PromptResponse()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.System),
            };
            modelMock.Setup(model => model.CompletePromptAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<IPromptFunctions<List<string>>>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptTemplate>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var augmentationMock = new Mock<IAugmentation>();
            var planMock = new Mock<Plan>();
            augmentationMock.Setup(augmentation => augmentation.CreatePlanFromResponseAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<PromptResponse>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(planMock.Object);
            augmentationMock.Setup(augmentation => augmentation.ValidateResponseAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<ITurnState>(),
                It.IsAny<ITokenizer>(),
                It.IsAny<PromptResponse>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new Validation { Valid = true });
            promptTemplate.Augmentation = augmentationMock.Object;
            var prompts = new PromptManager();
            prompts.AddPrompt("prompt", promptTemplate);
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );
            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var state = new TurnState(storage);
            await state.LoadStateAsync(turnContext);
            state.Temp.SetValue("input", "test");
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());
            var ai = new AISystem(new(planner));

            // Act
            var result = await planner.BeginTaskAsync(turnContext, state, ai);

            // Assert
            Assert.Equal(planMock.Object, result);
        }

        [Fact]
        public void Test_Get_Model()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var prompts = new PromptManager();
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());

            // Act
            var result = planner.Model;

            // Assert
            Assert.Equal(options.Model, result);
        }

        [Fact]
        public void Test_Get_Prompts()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var prompts = new PromptManager();
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());

            // Act
            var result = planner.Prompts;

            // Assert
            Assert.Equal(options.Prompts, result);
        }

        [Fact]
        public void Test_Get_DefaultPrompt()
        {
            // Arrange
            var modelMock = new Mock<IPromptCompletionModel>();
            var prompts = new PromptManager();
            var promptTemplate = new PromptTemplate(
                "prompt",
                new(new() { })
            );
            var options = new ActionPlannerOptions<ITurnState>(
                modelMock.Object,
                prompts,
                (context, state, planner) => Task.FromResult(promptTemplate)
            );
            var planner = new ActionPlanner<ITurnState>(options, new TestLoggerFactory());

            // Act
            var result = planner.DefaultPrompt;

            // Assert
            Assert.Equal(options.DefaultPrompt, result);
        }
    }
}