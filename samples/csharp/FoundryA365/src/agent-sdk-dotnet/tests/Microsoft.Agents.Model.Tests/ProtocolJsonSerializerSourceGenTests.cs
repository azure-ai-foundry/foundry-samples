// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Core.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ProtocolJsonSerializerSourceGenTests
    {
        [Fact]
        public void AddTypeInfoResolver_SetsNonNullResolver()
        {
            // Arrange
            var resolver = new DefaultJsonTypeInfoResolver();

            // Act
            ProtocolJsonSerializer.AddTypeInfoResolver(resolver);

            // Assert
            Assert.NotNull(ProtocolJsonSerializer.SerializationOptions.TypeInfoResolver);
        }

        [Fact]
        public void AddTypeInfoResolver_NewResolverConsultedFirst()
        {
            // Arrange: a resolver that handles HeroCard and tracks if it was called
            var wasCalled = false;
            var trackingResolver = new TrackingTypeInfoResolver(
                typeof(Microsoft.Agents.Core.Models.HeroCard),
                () => wasCalled = true);

            // Act
            ProtocolJsonSerializer.AddTypeInfoResolver(trackingResolver);
            var json = """{"title":"Test"}""";
            JsonSerializer.Deserialize<Microsoft.Agents.Core.Models.HeroCard>(
                json, ProtocolJsonSerializer.SerializationOptions);

            // Assert
            Assert.True(wasCalled, "The most-recently registered resolver should be consulted first");
        }

        [Fact]
        public async Task AddTypeInfoResolver_ConcurrentCalls_DoNotThrow()
        {
            // Snapshot options so we can restore after the concurrent calls
            var savedOptions = ProtocolJsonSerializer.SerializationOptions;
            try
            {
                // Act: 20 concurrent calls with harmless resolvers
                var tasks = Enumerable.Range(0, 20).Select(_ =>
                    Task.Run(() =>
                        ProtocolJsonSerializer.AddTypeInfoResolver(new DefaultJsonTypeInfoResolver())));

                // Assert: no exceptions
                await Task.WhenAll(tasks);
            }
            finally
            {
                // Restore global state to avoid contaminating other tests
                ProtocolJsonSerializer.ApplyExtensionOptions(_ => savedOptions);
            }
        }

        [Fact]
        public void SerializationOptions_TypeInfoResolver_IsNonNull()
        {
            // After initialization, TypeInfoResolver must be set (not null)
            // so that CoreJsonContext metadata is used instead of reflection for covered types.
            Assert.NotNull(ProtocolJsonSerializer.SerializationOptions.TypeInfoResolver);
        }

        [Fact]
        public void SerializationOptions_TypeInfoResolver_CanResolveHeroCard()
        {
            // HeroCard is in CoreJsonContext — the resolver chain must return type info for it
            var typeInfo = ProtocolJsonSerializer.SerializationOptions
                .TypeInfoResolver?
                .GetTypeInfo(typeof(Microsoft.Agents.Core.Models.HeroCard),
                             ProtocolJsonSerializer.SerializationOptions);

            Assert.NotNull(typeInfo);
        }

        [Fact]
        public void HeroCard_SerializesAndDeserializes_ViaSourceGen()
        {
            // HeroCard has no custom converter — uses CoreJsonContext path
            var card = new Microsoft.Agents.Core.Models.HeroCard
            {
                Title = "Test Title",
                Subtitle = "Test Subtitle",
                Text = "Some text"
            };

            var json = ProtocolJsonSerializer.ToJson(card);
            var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.HeroCard>(json);

            Assert.Equal("Test Title", result.Title);
            Assert.Equal("Test Subtitle", result.Subtitle);
            Assert.Equal("Some text", result.Text);
        }

        [Fact]
        public void TokenExchangeResource_RoundTrips()
        {
            // Another non-converter type
            var resource = new Microsoft.Agents.Core.Models.TokenExchangeResource
            {
                Id = "resource-id",
                Uri = "https://example.com/token",
                ProviderId = "provider-1"
            };

            var json = ProtocolJsonSerializer.ToJson(resource);
            var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.TokenExchangeResource>(json);

            Assert.Equal("resource-id", result.Id);
            Assert.Equal("https://example.com/token", result.Uri);
            Assert.Equal("provider-1", result.ProviderId);
        }

        [Fact]
        public void ConversationParameters_Members_IReadOnlyList_Deserializes()
        {
            // ConversationParameters.Members is IReadOnlyList<ChannelAccount>
            var json = """{"members":[{"id":"user1","name":"User One"},{"id":"user2","name":"User Two"}]}""";

            var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.ConversationParameters>(json);

            Assert.NotNull(result.Members);
            Assert.Equal(2, result.Members.Count);
            Assert.Equal("user1", result.Members[0].Id);
            Assert.Equal("User Two", result.Members[1].Name);
        }

        [Fact]
        public void Activity_CustomConverter_StillTakesPriority()
        {
            // Activity is handled by ActivityConverter — it must take priority over CoreJsonContext.
            // We verify Activity still round-trips correctly via the converter path.
            var json = """{"type":"message","text":"hello world","id":"act-123"}""";

            var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.Activity>(json);

            Assert.Equal("message", result.Type);
            Assert.Equal("hello world", result.Text);
            Assert.Equal("act-123", result.Id);
        }

        [Fact]
        public void ChannelAccount_CustomConverter_StillTakesPriority()
        {
            // ChannelAccount is handled by ChannelAccountConverter.
            var json = """{"id":"user-1","name":"Alice","role":"user"}""";

            var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.ChannelAccount>(json);

            Assert.Equal("user-1", result.Id);
            Assert.Equal("Alice", result.Name);
        }

        [Fact]
        public void HeroCard_NullProperties_OmittedInJson()
        {
            // Verify DefaultIgnoreCondition = WhenWritingNull is respected via CoreJsonContext
            var card = new Microsoft.Agents.Core.Models.HeroCard
            {
                Title = "My Card"
                // Subtitle, Text, etc. are null — should be omitted
            };

            var json = ProtocolJsonSerializer.ToJson(card);

            Assert.Contains("\"title\"", json);
            Assert.DoesNotContain("subtitle", json);
            Assert.DoesNotContain("text", json);
        }

        [Fact]
        public void HeroCard_PropertyNameCamelCase_IsRespected()
        {
            // Verify PropertyNamingPolicy = CamelCase
            var card = new Microsoft.Agents.Core.Models.HeroCard { Title = "CamelTest" };

            var json = ProtocolJsonSerializer.ToJson(card);

            Assert.Contains("\"title\"", json);          // camelCase
            Assert.DoesNotContain("\"Title\"", json);    // not PascalCase
        }

        [Fact]
        public void ResourceResponse_RoundTrips()
        {
            // ResourceResponse is in CoreJsonContext (no custom converter)
            var original = new Microsoft.Agents.Core.Models.ResourceResponse { Id = "resp-42" };

            var json = ProtocolJsonSerializer.ToJson(original);
            var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.ResourceResponse>(json);

            Assert.Equal("resp-42", result.Id);
        }

        // Helper: a resolver that calls an action when it handles the target type
        private sealed class TrackingTypeInfoResolver : IJsonTypeInfoResolver
        {
            private readonly System.Type _targetType;
            private readonly System.Action _onResolved;
            private readonly DefaultJsonTypeInfoResolver _inner = new();

            public TrackingTypeInfoResolver(System.Type targetType, System.Action onResolved)
            {
                _targetType = targetType;
                _onResolved = onResolved;
            }

            public JsonTypeInfo? GetTypeInfo(System.Type type, JsonSerializerOptions options)
            {
                if (type == _targetType)
                    _onResolved();
                return _inner.GetTypeInfo(type, options);
            }
        }
    }
}
