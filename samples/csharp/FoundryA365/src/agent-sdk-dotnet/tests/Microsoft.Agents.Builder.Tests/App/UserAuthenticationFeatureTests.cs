// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class UserAuthenticationFeatureTests
    {
        private const string GraphName = "graph";
        private const string SharePointName = "sharepoint";
        private const string GraphToken = "graph token";
        private const string SharePointToken = "sharePoint token";
        private Mock<IUserAuthorization> MockGraph;
        private Mock<IUserAuthorization> MockSharePoint;
        private Mock<IChannelAdapter> MockChannelAdapter;
        private Mock<IConnections> MockConnections;

        public UserAuthenticationFeatureTests()
        {
            MockGraph = new Mock<IUserAuthorization>();
            MockGraph
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) }));
            MockGraph
                .Setup(e => e.Name)
                .Returns(GraphName);

            MockSharePoint = new Mock<IUserAuthorization>();
            MockSharePoint
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = SharePointToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) }));
            MockSharePoint
                .Setup(e => e.Name)
                .Returns(SharePointName);

            MockChannelAdapter = new Mock<IChannelAdapter>();

            MockConnections = new Mock<IConnections>();
        }

        [Fact]
        public void Test_DefaultHandlerNameNotFound()
        {
            Assert.Throws<IndexOutOfRangeException>(() => new AgentApplication(new AgentApplicationOptions((IStorage) null) 
            { 
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, new MemoryStorage(), MockConnections.Object, MockGraph.Object) 
                { 
                    DefaultHandlerName = "notfound" 
                } 
            }));
        }

        [Fact]
        public void Test_DefaultHandlerFirst()
        {
            var app = new AgentApplication(new AgentApplicationOptions((IStorage) null)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, new MemoryStorage(), MockConnections.Object, MockGraph.Object)
            });

            Assert.Equal(GraphName, app.UserAuthorization.DefaultHandlerName);
        }

        [Fact]
        public async Task Test_AutoSignIn_Default()
        {
            // arrange
            var options = new TestApplicationOptions((IStorage)null) 
            { 
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, new MemoryStorage(), MockConnections.Object, MockGraph.Object, MockSharePoint.Object) 
            };
            var app = new TestApplication(options);

            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var response = await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState);

            // assert
            Assert.True(response);
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName));
            Assert.Equal(GraphToken, await app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName));
        }

        [Fact]
        public async Task Test_AutoSignIn_Named()
        {
            // arrange
            var storage = new MemoryStorage();
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, MockGraph.Object, MockSharePoint.Object)
            };
            var app = new TestApplication(options);

            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var signInComplete = await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState, SharePointName);

            // assert
            Assert.True(signInComplete);
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, SharePointName));
            Assert.Equal(SharePointToken, await app.UserAuthorization.GetTurnTokenAsync(turnContext, SharePointName));
        }

        [Fact]
        public async Task Test_AutoSignIn_Pending()
        {
            MockGraph
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((TokenResponse)null));

            var storage = new MemoryStorage();
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, MockGraph.Object)
            };
            var app = new TestApplication(options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var signInComplete = await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState);

            // assert
            Assert.False(signInComplete);
        }

        [Fact]
        public async Task Test_SignOut_DefaultHandler()
        {
            // arrange
            var storage = new MemoryStorage();
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, MockGraph.Object, MockSharePoint.Object)
            };
            var app = new TestApplication(options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState);
            await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState, SharePointName);
            await app.UserAuthorization.SignOutUserAsync(turnContext, turnState);

            // assert
            Assert.Null(await app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName));
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, SharePointName));
        }

        [Fact]
        public async Task Test_SignOut_SpecificHandler()
        {
            // arrange
            var storage = new MemoryStorage();
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, MockGraph.Object, MockSharePoint.Object)
            };
            var app = new TestApplication(options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState);
            await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState, SharePointName);
            await app.UserAuthorization.SignOutUserAsync(turnContext, turnState, SharePointName);

            // assert
            Assert.Null(await app.UserAuthorization.GetTurnTokenAsync(turnContext, SharePointName));
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName));
        }

#if MANUAL_SIGNIN
        [Fact]
        public async Task Test_ManualSignInOnSuccessForCached()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // a IUserAuthorization that already has a token (user already signed in)
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) }));
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions(storage)
            {
                Adapter = adapter,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOff
                }
            };
            var app = new TestApplication(options);

            app.OnMessage("/signin", async (turnContext, turnState, cancellationToken) =>
            {
                await app.UserAuthorization.SignInUserAsync(turnContext, turnState, GraphName);
            });

            app.UserAuthorization.OnUserSignInSuccess(async (turnContext, turnState, handlerName, token, activity, CancellationToken) =>
            {
                await turnContext.SendActivityAsync($"sign in success for '{handlerName}' and you said '{activity.Text}'", cancellationToken: CancellationToken.None);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("/signin")
            .AssertReply($"sign in success for '{GraphName}' and you said '/signin'")
            .StartTestAsync();

            // assert
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(null, null, GraphName));
        }

        [Fact]
        public async Task Test_ManualSignInOnSuccessForFlow()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // mock IUserAuthorization that returns null the first attempt, then a token after that.
            int attempt = 0;
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (attempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions(storage)
            {
                Adapter = adapter,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOff
                }
            };
            var app = new TestApplication(options);

            app.OnMessage("/signin", async (turnContext, turnState, cancellationToken) =>
            {
                await app.UserAuthorization.SignInUserAsync(turnContext, turnState, GraphName);
            }); 

            app.UserAuthorization.OnUserSignInSuccess(async (turnContext, turnState, handlerName, token, activity, CancellationToken) =>
            {
                await turnContext.SendActivityAsync($"sign in success for '{handlerName}' and you said '{activity.Text}'", cancellationToken: CancellationToken.None);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("/signin")
            .Send("magic code")
            .AssertReply($"sign in success for '{GraphName}' and you said '/signin'")
            .StartTestAsync();

            // assert
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(null, null, GraphName));
        }
#endif

        [Fact]
        public async Task Test_AutoSignInForCached()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) }));
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny
                }
            };
            var app = new TestApplication(options);

            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("first message")
            .AssertReply("You said: first message")
            .StartTestAsync();

            // assert
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(MockTurnContext(), GraphName));
        }

        [Fact]
        public async Task Test_AutoSignInForFlow()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // mock IUserAuthorization that returns null the first attempt, then a token after that.
            int attempt = 0;
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (attempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny
                }
            };
            var app = new TestApplication(options);

            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("first message")
            .Send("magic code")
            .AssertReply("You said: first message")
            .StartTestAsync();

            // assert
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(MockTurnContext(), GraphName));
        }

        [Fact]
        public async Task Test_PerRoute_Single_AutoOff()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // mock IUserAuthorization that returns null the first attempt, then a token after that.
            // This simulate the "not signed in" initial state.
            int attempt = 0;
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (attempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            int sharePointAttempt = 0;
            var sharePointMock = new Mock<IUserAuthorization>();
            sharePointMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (sharePointAttempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = SharePointToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) });
                });
            sharePointMock
                .Setup(e => e.Name)
                .Returns(SharePointName);

            // an already signed in handler
            var signedInMock = new Mock<IUserAuthorization>();
            signedInMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return Task.FromResult(new TokenResponse() { Token = "signedIn token", Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) });
                });
            signedInMock
                .Setup(e => e.Name)
                .Returns("signedIn");


            // arrange
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, graphMock.Object, sharePointMock.Object, signedInMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOff
                }
            };
            var app = new TestApplication(options);

            // Route Handler for "-graph" using GraphName OAuth
            app.OnMessage("-graph", async (turnContext, turnState, cancellationToken) =>
            {
                Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName));
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            },
            autoSignInHandlers: [GraphName]);

            // Route Handler for "-sharepoint" using SharePointName OAuth
            app.OnMessage("-sharepoint", async (turnContext, turnState, cancellationToken) =>
            {
                Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, SharePointName));
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            },
            autoSignInHandlers: [SharePointName]);

            // Route Handler for "-signedIn" using "signedIn" OAuth.  This tests "already signed in".
            app.OnMessage("-signedIn", async (turnContext, turnState, cancellationToken) =>
            {
                Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, "signedIn"));
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            },
            autoSignInHandlers: ["signedIn"]);

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("-graph")
            .Send("magic code")
            .AssertReply("You said: -graph")
            .Send("-sharepoint")
            .Send("magic code")
            .AssertReply("You said: -sharepoint")
            .Send("-signedIn")
            .AssertReply("You said: -signedIn")
            .StartTestAsync();
        }

        [Fact]
        public async Task Test_PerRoute_Multi_AutoOff()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // mock IUserAuthorization that returns null the first attempt, then a token after that.
            // This simulate the "not signed in" initial state.
            int attempt = 0;
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (attempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            int sharePointAttempt = 0;
            var sharePointMock = new Mock<IUserAuthorization>();
            sharePointMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (sharePointAttempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = SharePointToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) });
                });
            sharePointMock
                .Setup(e => e.Name)
                .Returns(SharePointName);

            // arrange
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, graphMock.Object, sharePointMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOff
                }
            };
            var app = new TestApplication(options);

            // Route Handler for "-signin" which needs GraphName and SharePointName handlers.
            app.OnMessage("-signin", async (turnContext, turnState, cancellationToken) =>
            {
                Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName));
                Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, SharePointName));
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            },
            autoSignInHandlers: [SharePointName, GraphName]);

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("-signin")
            .Send("magic code")  // signing for GraphName
            .Send("magic code")  // signing for SharePointName
            .AssertReply("You said: -signin")
            .StartTestAsync();
        }

        // Auto on, Route with single autoHandlerName:  Route has access to DefaultHandler token, and per-route token
        [Fact]
        public async Task Test_PerRoute_AutoOn()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // mock IUserAuthorization that returns null the first attempt, then a token after that.
            // This simulate the "not signed in" initial state.
            int attempt = 0;
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (attempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // second mock IUserAuthorization that returns null the first attempt, then a token after that.
            // This simulate the "not signed in" initial state.
            int sharePointAttempt = 0;
            var sharePointMock = new Mock<IUserAuthorization>();
            sharePointMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (sharePointAttempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = SharePointToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) });
                });
            sharePointMock
                .Setup(e => e.Name)
                .Returns(SharePointName);

            // Setup app
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, graphMock.Object, sharePointMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny
                }
            };
            var app = new TestApplication(options);

            // Default AutoSignIn "global" handler.
            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                Assert.NotNull(app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName));
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            },
            rank: RouteRank.Last);

            // Route Handler for "-sharepoint" using SharePointName OAuth
            app.OnMessage("-sharepoint", async (turnContext, turnState, cancellationToken) =>
            {
                // SharePointName token because it was on the Route
                Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, SharePointName));

                // GraphName token should be available since it was global auto
                Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName));

                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            },
            autoSignInHandlers: [SharePointName]);

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("hi")   // test "global" AutoSignIn
            .Send("magic code")
            .AssertReply("You said: hi")
            .Send("-sharepoint")  // test per-route Auto
            .Send("magic code")
            .AssertReply("You said: -sharepoint")
            .StartTestAsync();
        }

        // Auto on, Route with multi autoHandlerName (one being the DefaultHandler):  Route has access to DefaultHandler token, and per-route token
        [Fact]
        public async Task Test_PerRoute_AutoOn_Overlapping()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // mock IUserAuthorization that returns null the first attempt, then a token after that.
            // This simulate the "not signed in" initial state.
            int attempt = 0;
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (attempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // second mock IUserAuthorization that returns null the first attempt, then a token after that.
            // This simulate the "not signed in" initial state.
            int sharePointAttempt = 0;
            var sharePointMock = new Mock<IUserAuthorization>();
            sharePointMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (sharePointAttempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = SharePointToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) });
                });
            sharePointMock
                .Setup(e => e.Name)
                .Returns(SharePointName);

            // Setup app
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, graphMock.Object, sharePointMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny
                }
            };
            var app = new TestApplication(options);

            // Default AutoSignIn "global" handler.
            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName));
                Assert.Null(await app.UserAuthorization.GetTurnTokenAsync(turnContext, SharePointName));
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            },
            rank: RouteRank.Last);

            // Route Handler for "-sharepoint" using SharePointName OAuth
            app.OnMessage("-sharepoint", async (turnContext, turnState, cancellationToken) =>
            {
                // SharePointName token because it was on the Route
                Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, SharePointName));

                // GraphName token should be available since it was global auto
                Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName));

                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            },
            autoSignInHandlers: [GraphName, SharePointName]);

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("hi")   // test "global" AutoSignIn
            .Send("magic code")
            .AssertReply("You said: hi")
            .Send("-sharepoint")  // test per-route Auto
            .Send("magic code")
            .AssertReply("You said: -sharepoint")
            .StartTestAsync();
        }

        // tests UserAuthentication.ExchangeTurnToken calls IUserAuthorization.GetRefreshedUserTokenAsync for an exchangeable token (regardless of expiration)
        [Fact]
        public async Task Test_AutoWithOBO_SkipCache()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // mock IUserAuthorization that returns null the first attempt, then a token after that.
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30), IsExchangeable = true });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);
            graphMock
                .Setup(e => e.GetRefreshedUserTokenAsync(It.IsAny<ITurnContext>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return Task.FromResult(new TokenResponse() { Token = "NewGraphToken", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) });
                });

            // arrange
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny
                }
            };
            var app = new TestApplication(options);

            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                var token = await app.UserAuthorization.ExchangeTurnTokenAsync(turnContext, GraphName);
                Assert.NotNull(token);
                Assert.Equal("NewGraphToken", token);
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("first message")
            .AssertReply("You said: first message")
            .StartTestAsync();

            // assert
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(MockTurnContext(), GraphName));
        }

        [Fact]
        public async Task Test_AutoWithOBO_ConfigExchange()
        {
            var userTokenClient = new Mock<IUserTokenClient>();
            userTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenOrSignInResourceResponse() { TokenResponse = new TokenResponse() { Token = "exchangeableToken", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30), IsExchangeable = true } }));

            // "oboConnection" provider
            IList<string> oboScopes = ["oboScopes"];

            var accessTokenProvider = new Mock<IAccessTokenProvider>();
            accessTokenProvider
                .Setup(p => p.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<String>>(), It.IsAny<bool>()))
                .Returns(Task.FromResult("NewToken"));

            var oboExchangeProvider = accessTokenProvider.As<IOBOExchange>();
            oboExchangeProvider
                .Setup(o => o.AcquireTokenOnBehalfOf(oboScopes, "exchangeableToken"))
                .Returns(Task.FromResult(new TokenResponse() { Token = "NewToken", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) }));

            var connections = new ConfigurationConnections(new Dictionary<string, IAccessTokenProvider> { { "oboConnection", accessTokenProvider.Object } }, null);

            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter(tokenClient: userTokenClient.Object);

            // handler "graph" with both OBO values
            var handler = new AzureBotUserAuthorization(GraphName, storage, connections, new OAuthSettings() { OBOConnectionName = "oboConnection", OBOScopes = oboScopes });

            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, connections, handler)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny
                }
            };
            var app = new TestApplication(options);

            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                var token = await app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName);
                Assert.NotNull(token);
                Assert.Equal("NewToken", token);
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("first message")
            .AssertReply("You said: first message")
            .StartTestAsync();
        }

        [Fact]
        public async Task Test_AutoWithOBO_RuntimeScopes()
        {
            var userTokenClient = new Mock<IUserTokenClient>();
            userTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenOrSignInResourceResponse() { TokenResponse = new TokenResponse() { Token = "exchangeableToken", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30), IsExchangeable = true } }));
            userTokenClient
                .Setup(c => c.GetUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = "exchangeableToken", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30), IsExchangeable = true }));

            // "oboConnection" provider
            IList<string> runtimeOboScopes = ["oboScopes"];

            var accessTokenProvider = new Mock<IAccessTokenProvider>();
            accessTokenProvider
                .Setup(p => p.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<String>>(), It.IsAny<bool>()))
                .Returns(Task.FromResult("NewToken"));

            var oboExchangeProvider = accessTokenProvider.As<IOBOExchange>();
            oboExchangeProvider
                .Setup(o => o.AcquireTokenOnBehalfOf(runtimeOboScopes, "exchangeableToken"))
                .Returns(Task.FromResult(new TokenResponse() { Token = "NewToken", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) }));

            var connections = new ConfigurationConnections(new Dictionary<string, IAccessTokenProvider> { { "oboConnection", accessTokenProvider.Object } }, null);

            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter(tokenClient: userTokenClient.Object);

            // handler "graph" with both OBO connection name only
            var handler = new AzureBotUserAuthorization(GraphName, storage, connections, new OAuthSettings() { OBOConnectionName = "oboConnection" });

            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, connections, handler)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny
                }
            };
            var app = new TestApplication(options);

            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                var token = await app.UserAuthorization.ExchangeTurnTokenAsync(turnContext, GraphName, exchangeScopes: runtimeOboScopes);
                Assert.NotNull(token);
                Assert.Equal("NewToken", token);
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("first message")
            .AssertReply("You said: first message")
            .StartTestAsync();
        }

        // token refreshes within 5 minutes of expiration
        [Fact]
        public async Task Test_Auto_GetTurnTokenRefresh()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);
            graphMock
                .Setup(e => e.GetRefreshedUserTokenAsync(It.IsAny<ITurnContext>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    // shouldn't get called
                    return Task.FromResult(new TokenResponse() { Token = null, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) });
                });

            var graphRefreshMock = new Mock<IUserAuthorization>();
            graphRefreshMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return Task.FromResult(new TokenResponse() { Token = "OldToken", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(4) });
                });
            graphRefreshMock
                .Setup(e => e.Name)
                .Returns("RefreshGraphName");
            graphRefreshMock
                .Setup(e => e.GetRefreshedUserTokenAsync(It.IsAny<ITurnContext>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return Task.FromResult(new TokenResponse() { Token = "NewGraphToken", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) });
                });

            // arrange
            var options = new TestApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, MockConnections.Object, graphMock.Object, graphRefreshMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny,
                    DefaultHandlerName = GraphName
                }
            };
            var app = new TestApplication(options);

            // auto
            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                var token = await app.UserAuthorization.GetTurnTokenAsync(turnContext, GraphName);
                Assert.NotNull(token);
                Assert.Equal(GraphToken, token);
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            }, rank: RouteRank.Last);

            // should get a new token (calls GetRefreshedUserTokenAsync)
            app.OnMessage("refresh", async (turnContext, turnState, cancellationToken) =>
            {
                var token = await app.UserAuthorization.GetTurnTokenAsync(turnContext, "RefreshGraphName");
                Assert.NotNull(token);
                Assert.Equal("NewGraphToken", token);
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            }, autoSignInHandlers: ["RefreshGraphName"]);

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("first message")
            .AssertReply("You said: first message")
            .Send("refresh")
            .AssertReply("You said: refresh")
            .StartTestAsync();

            // assert
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenAsync(MockTurnContext(), GraphName));
        }

        private static TurnContext MockTurnContext()
        {
            return new TurnContext(new SimpleAdapter(), new Activity()
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
        }
    }
}
