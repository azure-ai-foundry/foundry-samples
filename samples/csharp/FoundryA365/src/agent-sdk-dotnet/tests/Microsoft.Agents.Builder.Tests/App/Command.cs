// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Threading.Tasks;
using System.Threading;
using Xunit;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Storage;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class CommandActivity
    {
        [Fact]
        public async Task CommandBotTest()
        {
            var adapter = new TestAdapter();
            var storage = new MemoryStorage();

            // Create mock Activity for testing.
            var commandActivity = new Activity
            {
                Type = ActivityTypes.Command,
                Name = "channel/vnd.microsoft.test.multiply",
                Value = new MathCommand { First = 2, Second = 2 }
            };

            var unknownCommandActivity = new Activity
            {
                Type = ActivityTypes.Command,
                Name = "channel/vnd.microsoft.test.divide",
                Value = new MathCommand { First = 10, Second = 2 }
            };

            await new TestFlow(adapter, new CommandBot(new AgentApplicationOptions(storage)))
                .Send(commandActivity)
                .AssertReply((activity) =>
                {
                    Assert.Equal(commandActivity.Name, activity.Name);

                    var result = ProtocolJsonSerializer.ToObject<CommandResultValue<MathResult>>(activity.Value);
                    Assert.Equal(4, result.Data.Result);
                })
                .Send(unknownCommandActivity)
                .AssertReply((activity) =>
                {
                    Assert.Equal(unknownCommandActivity.Name, activity.Name);

                    var result = ProtocolJsonSerializer.ToObject<CommandResultValue<MathResult>>(activity.Value);
                    Assert.Equal("NotSupported", result.Error.Code);
                })
                .StartTestAsync();
        }
    }

    class CommandBot : AgentApplication
    {
        public CommandBot(AgentApplicationOptions options) : base(options) 
        {
            OnActivity(ActivityTypes.Command, OnCommandAsync);
        }

        public static async Task OnCommandAsync(ITurnContext turnContext, ITurnState state, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Name == "channel/vnd.microsoft.test.multiply")
            {
                var value = ProtocolJsonSerializer.ToObject<MathCommand>(turnContext.Activity.Value);

                var commandResult = new Activity()
                {
                    Type = "commandResult",
                    Name = turnContext.Activity.Name,
                    Value = new CommandResultValue<MathResult>
                    {
                        Data = new MathResult { Result = value.First * value.Second }
                    }
                };

                await turnContext.SendActivityAsync(commandResult, cancellationToken);
            }
            else
            {
                var commandResult = new Activity()
                {
                    Type = "commandResult",
                    Name = turnContext.Activity.Name,
                    Value = new CommandResultValue<MathResult>
                    {
                        Error = new Error
                        {
                            Code = "NotSupported"
                        }
                    }
                };

                await turnContext.SendActivityAsync(commandResult, cancellationToken);
            }
        }
    }

    class MathCommand
    {
        public int First;
        public int Second;
    }

    class MathResult
    {
        public int Result;
    }
}