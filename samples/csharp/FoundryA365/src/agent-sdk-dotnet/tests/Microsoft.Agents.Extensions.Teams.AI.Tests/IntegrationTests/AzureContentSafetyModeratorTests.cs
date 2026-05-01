using Microsoft.Agents.Extensions.Teams.AI.Moderator;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using Microsoft.Agents.Extensions.Teams.AI;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Reflection;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.IntegrationTests
{
    public sealed class AzureContentSafetyModeratorTests
    {
        private readonly IConfigurationRoot _configuration;

        public AzureContentSafetyModeratorTests()
        {
            var currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (string.IsNullOrWhiteSpace(currentAssemblyDirectory))
            {
                throw new InvalidOperationException("Unable to determine current assembly directory.");
            }

            var directoryPath = Path.GetFullPath(Path.Combine(currentAssemblyDirectory, $"../../../IntegrationTests/"));
            var settingsPath = Path.Combine(directoryPath, "testsettings.json");

            _configuration = new ConfigurationBuilder()
                .AddJsonFile(path: settingsPath, optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<AzureContentSafetyModeratorTests>()
                .Build();
        }

        [Theory(Skip = "This test should only be run manually.")]
        [InlineData("I hate you", true)]
        [InlineData("Turn on the light", false)]
        [InlineData("我恨你", true)]
        [InlineData("電気をつける", false)]
        public async Task AzureContentSafetyModerator_ReviewPrompt(string input, bool flagged)
        {
            // Arrange
            var config = _configuration.GetSection("AzureContentSafety").Get<AzureContentSafetyConfiguration>();
            var options = new AzureContentSafetyModeratorOptions(config?.ApiKey ?? throw new Exception("config Missing in AzureContentSafetyModerator_ReviewPrompt"), config?.Endpoint ?? throw new Exception("config Missing in AzureContentSafetyModerator_ReviewPrompt"), ModerationType.Both);
            var moderator = new AzureContentSafetyModerator<TurnState>(options);

            var botAdapterMock = new Mock<IChannelAdapter>();
            // TODO: when TurnState is implemented, get the user input
            var activity = new Activity()
            {
                Text = input,
            };
            var turnContext = new TurnContext(botAdapterMock.Object, activity);
            var turnStateMock = new Mock<TurnState>();

            // Act
            var result = await moderator.ReviewInputAsync(turnContext, turnStateMock.Object);

            // Assert
            if (flagged)
            {
                Assert.NotNull(result);
                Assert.Equal(AIConstants.DoCommand, result.Commands[0].Type);
                Assert.Equal(AIConstants.FlaggedInputActionName, ((PredictedDoCommand)result.Commands[0]).Action);
            }
            else
            {
                Assert.Null(result);
            }
        }

        [Theory(Skip = "This test should only be run manually.")]
        [InlineData("I hate you", true)]
        [InlineData("The light is turned on", false)]
        public async Task AzureContentSafetyModerator_ReviewPlan(string response, bool flagged)
        {
            // Arrange
            var config = _configuration.GetSection("AzureContentSafety").Get<AzureContentSafetyConfiguration>();
            var options = new AzureContentSafetyModeratorOptions(config?.ApiKey ?? throw new Exception("config Missing in AzureContentSafetyModerator_ReviewPlan"), config.Endpoint, ModerationType.Both);
            var moderator = new AzureContentSafetyModerator<TurnState>(options);

            var turnContextMock = new Mock<ITurnContext>();
            var turnStateMock = new Mock<TurnState>();
            var plan = new Plan(new List<IPredictedCommand>()
            {
                new PredictedSayCommand(response)
            });

            // Act
            var result = await moderator.ReviewOutputAsync(turnContextMock.Object, turnStateMock.Object, plan);

            // Assert
            if (flagged)
            {
                Assert.Equal(AIConstants.DoCommand, result.Commands[0].Type);
                Assert.Equal(AIConstants.FlaggedOutputActionName, ((PredictedDoCommand)result.Commands[0]).Action);
            }
            else
            {
                Assert.Equal(AIConstants.SayCommand, result.Commands[0].Type);
            }
        }
    }
}
