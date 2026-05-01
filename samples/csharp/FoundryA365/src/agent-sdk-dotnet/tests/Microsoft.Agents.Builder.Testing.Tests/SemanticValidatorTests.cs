// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace Microsoft.Agents.Builder.Testing
{
    public class SemanticValidatorTests
    {
        [Fact]
        public async Task ValidateAsync_YesResponse_Passes()
        {
            var validator = new SemanticValidator(
                CreateMockChatClient("yes"),
                "Does this echo the user?");
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello" };

            // Should not throw
            await validator.ValidateAsync(activity);
        }

        [Fact]
        public async Task ValidateAsync_YesWithWhitespace_Passes()
        {
            var validator = new SemanticValidator(
                CreateMockChatClient("  YES  "),
                "Does this echo the user?");
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello" };

            await validator.ValidateAsync(activity);
        }

        [Fact]
        public async Task ValidateAsync_NoResponse_ThrowsWithMessage()
        {
            const string prompt = "Does this echo the user?";
            var validator = new SemanticValidator(CreateMockChatClient("no"), prompt);
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello" };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => validator.ValidateAsync(activity));

            Assert.Contains("Semantic validation failed", ex.Message);
            Assert.Contains(prompt, ex.Message);
            Assert.Contains("hello", ex.Message);
        }

        [Fact]
        public async Task ValidateAsync_UnexpectedResponse_ThrowsWithMessage()
        {
            var validator = new SemanticValidator(
                CreateMockChatClient("maybe"),
                "Does this echo the user?");
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello" };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => validator.ValidateAsync(activity));

            Assert.Contains("unexpected LLM response", ex.Message);
            Assert.Contains("maybe", ex.Message);
        }

        [Fact]
        public async Task ValidateAsync_NullText_UsesNonTextPlaceholder()
        {
            // Arrange: mock returns "no" so we can inspect the message in the exception
            const string prompt = "Is this a text message?";
            var validator = new SemanticValidator(CreateMockChatClient("no"), prompt);
            var activity = new Activity { Type = ActivityTypes.Message, Text = null };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => validator.ValidateAsync(activity));

            Assert.Contains("(non-text activity)", ex.Message);
        }

        private static IChatClient CreateMockChatClient(string fixedResponse)
        {
            var mock = new Mock<IChatClient>();
            mock.Setup(c => c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, fixedResponse)));
            return mock.Object;
        }
    }
}
