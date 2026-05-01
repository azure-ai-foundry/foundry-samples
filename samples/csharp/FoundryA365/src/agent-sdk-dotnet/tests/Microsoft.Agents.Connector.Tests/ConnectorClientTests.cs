// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Moq;
using Xunit;

namespace Microsoft.Agents.Connector.Tests
{
    public class ConnectorClientTests
    {
        private const string ConversationId = "test_conversation";
        private const string UserId = "test_user";
        private const string BotId = "test_bot";
        private const string ServiceUrl = "https://test.botframework.com";
        private const string Audience = "test";
        private const string Token = "STUB_TOKEN";

        [Fact]
        public async Task SendActivityTestAsync()
        {
            var bot = new ChannelAccount { Id = BotId };
            var user = new ChannelAccount { Id = UserId };

            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Id = Guid.NewGuid().ToString("N"),
                From = bot,
                Recipient = user,
                Conversation = new ConversationAccount { Id = ConversationId },
                Text = "test"
            };

            Func<HttpRequestMessage, HttpResponseMessage> sendRequest = request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal($"{ServiceUrl}/v3/conversations/{ConversationId}/activities", request.RequestUri.AbsoluteUri);
                Assert.Equal($"Bearer {Token}", request.Headers.Authorization.ToString());

                var incoming = ProtocolJsonSerializer.ToObject<Activity>(request.Content.ReadAsStringAsync().Result);
                Assert.Equal(activity.Id, incoming.Id);

                var outgoing = new ResourceResponse
                {
                    Id = ConversationId
                };

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(ProtocolJsonSerializer.ToJson(outgoing), Encoding.UTF8, "application/json");

                return response;
            };

            using var handler = new MockClientHandler(sendRequest);
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(a => a.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(handler));

            var connectorClient = new RestConnectorClient(new Uri(ServiceUrl), httpFactory.Object, () => Task.FromResult<string>(Token));

            var result = await connectorClient.Conversations.SendToConversationAsync(activity);

            Assert.Equal(ConversationId, result.Id);
        }

        [Fact]
        public async Task ReplyToActivityTestAsync()
        {
            var bot = new ChannelAccount { Id = BotId };
            var user = new ChannelAccount { Id = UserId };

            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Id = Guid.NewGuid().ToString("N"),
                From = bot,
                Recipient = user,
                Conversation = new ConversationAccount { Id = ConversationId },
                ReplyToId = Guid.NewGuid().ToString("N"),
                Text = "test"
            };

            Func<HttpRequestMessage, HttpResponseMessage> sendRequest = request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal($"{ServiceUrl}/v3/conversations/{ConversationId}/activities/{activity.ReplyToId}", request.RequestUri.AbsoluteUri);
                Assert.Equal($"Bearer {Token}", request.Headers.Authorization.ToString());

                var incoming = ProtocolJsonSerializer.ToObject<Activity>(request.Content.ReadAsStringAsync().Result);
                Assert.Equal(activity.Id, incoming.Id);
                Assert.Equal(activity.ReplyToId, incoming.ReplyToId);

                var outgoing = new ResourceResponse
                {
                    Id = ConversationId
                };

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(ProtocolJsonSerializer.ToJson(outgoing), Encoding.UTF8, "application/json");

                return response;
            };

            using var handler = new MockClientHandler(sendRequest);
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(a => a.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(handler));

            var connectorClient = new RestConnectorClient(new Uri(ServiceUrl), httpFactory.Object, () => Task.FromResult<string>(Token));

            var result = await connectorClient.Conversations.ReplyToActivityAsync(activity);

            Assert.Equal(ConversationId, result.Id);
        }

        private class MockClientHandler : HttpClientHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;

            public MockClientHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
            {
                _send = send;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_send(request));
            }
        }

        private class MockTokenAccess : IAccessTokenProvider
        {
            public ImmutableConnectionSettings ConnectionSettings => throw new NotImplementedException();

            public Task<string> GetAccessTokenAsync(string resourceUrl, IList<string> scopes, bool forceRefresh = false)
            {
                return Task.FromResult(Token);
            }

            public TokenCredential GetTokenCredential()
            {
                throw new NotImplementedException();
            }
        }

        /*
        private class TestObjectClass
        {
            public string ObjectName { get; set; }

            public TestObjectClass TestObject { get; set; }
        }

        private class ChannelDataValueScenarios : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[]
                {
                    new ActivityTestScenario
                    {
                        TestData = new TestObjectClass
                        {
                            ObjectName = "LevelOne",
                            TestObject = new TestObjectClass
                            {
                                ObjectName = "LevelTwo"
                            }
                        },
                        Validator = actual =>
                        {
                            var actualData = actual.ToObject<TestObjectClass>();
                            Assert.NotNull(actualData);
                            Assert.Equal("LevelOne", actualData.ObjectName);
                            Assert.NotNull(actualData.TestObject);
                            Assert.Equal("LevelTwo", actualData.TestObject.ObjectName);
                        }
                    }
                };

                yield return new object[]
                {
                    new ActivityTestScenario
                    {
                        TestData = "foo",
                        Validator = actual =>
                        {
                            Assert.Equal("\"foo\"", actual);
                        }
                    }
                };

                yield return new object[]
                {
                    new ActivityTestScenario
                    {
                        TestData = 1,
                        Validator = actual =>
                        {
                            Assert.Equal(1, int.Parse((string)actual, CultureInfo.CurrentCulture));
                        }
                    }
                };

                yield return new object[]
                {
                    new ActivityTestScenario
                    {
                        TestData = true,
                        Validator = actual =>
                        {
                            Assert.Equal("true", actual);
                        }
                    }
                };

                yield return new object[]
                {
                    new ActivityTestScenario
                    {
                        TestData = false,
                        Validator = actual =>
                        {
                            Assert.Equal("false", actual);
                        }
                    }
                };

                yield return new object[]
                {
                    new ActivityTestScenario
                    {
                        TestData = new[]
                        {
                            new TestObjectClass
                            {
                                ObjectName = "LevelOneA",
                                TestObject = new TestObjectClass
                                {
                                    ObjectName = "LevelTwoA"
                                }
                            },
                            new TestObjectClass
                            {
                                ObjectName = "LevelOneB",
                                TestObject = new TestObjectClass
                                {
                                    ObjectName = "LevelTwoB"
                                }
                            }
                        },
                        Validator = actual =>
                        {
                            var element = (JsonElement)actual;
                            Assert.Equal(JsonValueKind.Array, element.ValueKind);
                            var elements = element.EnumerateArray().ToArray();

                            var dataA = elements[0].ToObject<TestObjectClass>();
                            Assert.NotNull(dataA);
                            Assert.Equal("LevelOneA", dataA.ObjectName);
                            Assert.NotNull(dataA.TestObject);
                            Assert.Equal("LevelTwoA", dataA.TestObject.ObjectName);

                            var dataB = elements[1].ToObject<TestObjectClass>();
                            Assert.NotNull(dataB);
                            Assert.Equal("LevelOneB", dataB.ObjectName);
                            Assert.NotNull(dataB.TestObject);
                            Assert.Equal("LevelTwoB", dataB.TestObject.ObjectName);
                        }
                    }
                };
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
        */
    }
}
