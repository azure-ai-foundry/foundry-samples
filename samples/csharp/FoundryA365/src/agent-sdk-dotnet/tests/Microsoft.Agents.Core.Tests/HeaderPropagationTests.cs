// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Core.HeaderPropagation.Tests
{
    [Collection("Non-Parallel Collection")] // Ensure this test runs in a single-threaded context to avoid issues with static dictionary.
    public class HeaderPropagationTests
    {
        public HeaderPropagationTests()
        {
            HeaderPropagationContext.HeadersToPropagate = new HeaderPropagationEntryCollection();
        }

        [Fact]
        public void HeaderPropagationContext_ShouldFilterHeaders()
        {
            // Arrange
            HeaderPropagationContext.HeadersToPropagate.Add("x-custom-header", "custom-value");
            HeaderPropagationContext.HeadersToPropagate.Propagate("x-custom-header-1");
            HeaderPropagationContext.HeadersToPropagate.Override("x-custom-header-2", "new-value");
            HeaderPropagationContext.HeadersToPropagate.Append("x-custom-header-3", "extra-value");

            HeaderPropagationContext.HeadersFromRequest = new Dictionary<string, StringValues>
            {
                { "x-ms-correlation-id", new StringValues("1234") },
                { "x-custom-header-1", new StringValues("Value-1") },
                { "x-custom-header-2", new StringValues("Value-2") },
                { "x-custom-header-3", new StringValues("Value-3") }
            };

            // Act
            var filteredHeaders = HeaderPropagationContext.HeadersFromRequest;

            // Assert
            Assert.Equal(5, filteredHeaders.Count);
            Assert.Equal("1234", filteredHeaders["x-ms-correlation-id"]);
            Assert.Equal("custom-value", filteredHeaders["x-custom-header"]);
            Assert.Equal("Value-1", filteredHeaders["x-custom-header-1"]);
            Assert.Equal("new-value", filteredHeaders["x-custom-header-2"]);
            Assert.Equal("Value-3,extra-value", filteredHeaders["x-custom-header-3"]);
        }

        [Fact]
        public void HeaderPropagationContext_ShouldAppendMultipleValues()
        {
            // Arrange
            HeaderPropagationContext.HeadersToPropagate.Append("User-Agent", "extra-value-1");
            HeaderPropagationContext.HeadersToPropagate.Append("User-Agent", "extra-value-2");
            HeaderPropagationContext.HeadersToPropagate.Append("Accept", "text/html");

            HeaderPropagationContext.HeadersFromRequest = new Dictionary<string, StringValues>
            {
                { "x-ms-correlation-id", new StringValues("1234") },
                { "User-Agent", new StringValues("Value-1") },
                { "Accept", new StringValues("text/plain") }
            };

            // Act
            var filteredHeaders = HeaderPropagationContext.HeadersFromRequest;

            // Assert
            Assert.Equal(3, filteredHeaders.Count);
            Assert.Equal("1234", filteredHeaders["x-ms-correlation-id"]);
            Assert.Equal("Value-1 extra-value-1 extra-value-2", filteredHeaders["User-Agent"]);
            Assert.Equal("text/plain,text/html", filteredHeaders["Accept"]);
        }

        [Fact]
        public void HeaderPropagationContext_MultipleAdd_ShouldKeepLastValue()
        {
            // Arrange
            HeaderPropagationContext.HeadersToPropagate.Add("x-custom-header-1", "value-1");
            HeaderPropagationContext.HeadersToPropagate.Add("x-custom-header-1", "value-2");

            HeaderPropagationContext.HeadersFromRequest = new Dictionary<string, StringValues>
            {
                { "x-ms-correlation-id", new StringValues("1234") },
            };

            // Act
            var filteredHeaders = HeaderPropagationContext.HeadersFromRequest;

            // Assert
            Assert.Equal(2, filteredHeaders.Count);
            Assert.Equal("1234", filteredHeaders["x-ms-correlation-id"]);
            Assert.Equal("value-2", filteredHeaders["x-custom-header-1"]);
        }

        [Fact]
        public void HeaderPropagationContext_MultipleOverride_ShouldKeepLastValue()
        {
            // Arrange
            HeaderPropagationContext.HeadersToPropagate.Override("x-custom-header-1", "new-value-1");
            HeaderPropagationContext.HeadersToPropagate.Override("x-custom-header-1", "new-value-2");

            HeaderPropagationContext.HeadersFromRequest = new Dictionary<string, StringValues>
            {
                { "x-ms-correlation-id", new StringValues("1234") },
                { "x-custom-header-1", new StringValues("Value-1") }
            };

            // Act
            var filteredHeaders = HeaderPropagationContext.HeadersFromRequest;

            // Assert
            Assert.Equal(2, filteredHeaders.Count);
            Assert.Equal("1234", filteredHeaders["x-ms-correlation-id"]);
            Assert.Equal("new-value-2", filteredHeaders["x-custom-header-1"]);
        }
    }

    [CollectionDefinition("Non-Parallel Collection", DisableParallelization = true)]
    public class NonParallelCollectionDefinition { }
}
