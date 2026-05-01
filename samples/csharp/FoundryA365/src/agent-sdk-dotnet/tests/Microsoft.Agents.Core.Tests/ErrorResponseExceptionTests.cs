// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using Xunit;

namespace Microsoft.Agents.Core.Tests
{
    public class ErrorResponseExceptionTests
    {
        private static readonly AgentErrorDefinition TestErrorDefinition = new AgentErrorDefinition(
            code: 12345,
            description: "Test error message",
            helplink: "https://test.com/help"
        );

        [Fact]
        public void CreateErrorResponseException_WithHttpResponse_FiltersSensitiveHeaders()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
            httpResponse.Headers.Add("X-Correlation-Id", "correlation-123");
            httpResponse.Headers.Add("X-Request-Id", "request-456");
            httpResponse.Headers.Add("Cache-Control", "no-cache");
            
            // Note: Authorization, WWW-Authenticate, Proxy-Authorization, Proxy-Authenticate are valid response headers
            // Set-Cookie and Cookie are actually content headers, not response headers in HttpResponseMessage
            // So we'll add them via TryAddWithoutValidation which allows any header
            httpResponse.Headers.TryAddWithoutValidation("Authorization", "Bearer secret-token");
            httpResponse.Headers.TryAddWithoutValidation("X-API-Key", "api-key-value");
            httpResponse.Headers.TryAddWithoutValidation("Set-Cookie", "session=secret");
            httpResponse.Headers.TryAddWithoutValidation("Cookie", "user=secret");

            // Act
            var exception = ErrorResponseException.CreateErrorResponseException(
                httpResponse,
                TestErrorDefinition,
                null,
                CancellationToken.None);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(TestErrorDefinition.code, exception.HResult);
            Assert.Equal((int)HttpStatusCode.BadRequest, exception.StatusCode);

            // Sensitive headers should NOT be in exception data
            Assert.False(exception.Data.Contains("Authorization"), "Authorization header should be filtered");
            Assert.False(exception.Data.Contains("X-API-Key"), "X-API-Key header should be filtered");
            Assert.False(exception.Data.Contains("Set-Cookie"), "Set-Cookie header should be filtered");
            Assert.False(exception.Data.Contains("Cookie"), "Cookie header should be filtered");

            // Non-sensitive headers should be in exception data
            // Note: HttpHeaders normalizes "X-Request-Id" to "X-Request-Id"
            Assert.True(exception.Data.Contains("X-Correlation-Id"), "X-Correlation-Id header should be present");
            Assert.Equal("correlation-123", exception.Data["X-Correlation-Id"]);
#if NETFRAMEWORK
                Assert.True(exception.Data.Contains("X-Request-Id"), "X-Request-Id header should be present (normalized from X-Request-Id)");
                Assert.Equal("request-456", exception.Data["X-Request-Id"]);
#else
            Assert.True(exception.Data.Contains("X-Request-ID"), "X-Request-Id header should be present (normalized from X-Request-Id)");
            Assert.Equal("request-456", exception.Data["X-Request-ID"]);
#endif
            Assert.True(exception.Data.Contains("Cache-Control"), "Cache-Control header should be present");
        }

        [Fact]
        public void CreateErrorResponseException_FiltersSensitiveHeaders_CaseInsensitive()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            httpResponse.Headers.TryAddWithoutValidation("authorization", "Bearer lowercase-token");
            httpResponse.Headers.TryAddWithoutValidation("x-api-key", "lowercase-api-key");
            httpResponse.Headers.Add("X-Custom-Header", "custom-value");

            // Act
            var exception = ErrorResponseException.CreateErrorResponseException(
                httpResponse,
                TestErrorDefinition,
                null,
                CancellationToken.None);

            // Assert
            // All variations of sensitive headers should be filtered
            Assert.False(exception.Data.Contains("authorization"), "authorization (lowercase) should be filtered");
            Assert.False(exception.Data.Contains("x-api-key"), "x-api-key (lowercase) should be filtered");

            // Non-sensitive headers should still be present
            Assert.True(exception.Data.Contains("X-Custom-Header"), "X-Custom-Header should be present");
        }

        [Fact]
        public void CreateErrorResponseException_FiltersAllSensitiveHeaderTypes()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.Forbidden);
            // Use TryAddWithoutValidation to add headers that might not be standard response headers
            httpResponse.Headers.TryAddWithoutValidation("Authorization", "Bearer token");
            httpResponse.Headers.TryAddWithoutValidation("X-Auth-Token", "auth-token");
            httpResponse.Headers.TryAddWithoutValidation("X-CSRF-Token", "csrf-token");
            httpResponse.Headers.TryAddWithoutValidation("WWW-Authenticate", "Basic realm=\"test\"");
            httpResponse.Headers.TryAddWithoutValidation("Proxy-Authorization", "proxy-auth");
            httpResponse.Headers.TryAddWithoutValidation("Proxy-Authenticate", "proxy-auth-method");

            // Act
            var exception = ErrorResponseException.CreateErrorResponseException(
                httpResponse,
                TestErrorDefinition,
                null,
                CancellationToken.None);

            // Assert
            // All sensitive header types should be filtered
            Assert.False(exception.Data.Contains("Authorization"), "Authorization should be filtered");
            Assert.False(exception.Data.Contains("X-Auth-Token"), "X-Auth-Token should be filtered");
            Assert.False(exception.Data.Contains("X-CSRF-Token"), "X-CSRF-Token should be filtered");
            Assert.False(exception.Data.Contains("WWW-Authenticate"), "WWW-Authenticate should be filtered");
            Assert.False(exception.Data.Contains("Proxy-Authorization"), "Proxy-Authorization should be filtered");
            Assert.False(exception.Data.Contains("Proxy-Authenticate"), "Proxy-Authenticate should be filtered");
        }

        [Fact]
        public void CreateErrorResponseException_IncludesNonSensitiveHeaders()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            httpResponse.Headers.Add("X-Correlation-Id", "correlation-789");
            httpResponse.Headers.Add("X-Request-Id", "request-abc");
            httpResponse.Headers.Add("User-Agent", "TestAgent/1.0");
            httpResponse.Headers.Add("Accept", "application/json");

            // Act
            var exception = ErrorResponseException.CreateErrorResponseException(
                httpResponse,
                TestErrorDefinition,
                null,
                CancellationToken.None);

            // Assert
            // All non-sensitive headers should be present
            // Note: HttpHeaders normalizes "X-Request-Id" to "X-Request-Id"
            Assert.True(exception.Data.Contains("X-Correlation-Id"), "X-Correlation-Id should be present");
            Assert.Equal("correlation-789", exception.Data["X-Correlation-Id"]);
#if NETFRAMEWORK
                Assert.True(exception.Data.Contains("X-Request-Id"), "X-Request-Id should be present (normalized from X-Request-Id)");
                Assert.Equal("request-abc", exception.Data["X-Request-Id"]);
#else
            Assert.True(exception.Data.Contains("X-Request-ID"), "X-Request-Id should be present (normalized from X-Request-Id)");
            Assert.Equal("request-abc", exception.Data["X-Request-ID"]);
#endif
            Assert.True(exception.Data.Contains("User-Agent"), "User-Agent should be present");
            Assert.True(exception.Data.Contains("Accept"), "Accept should be present");
        }

        [Fact]
        public void CreateErrorResponseException_WithNullHeaders_DoesNotThrow()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);

            // Act
            var exception = ErrorResponseException.CreateErrorResponseException(
                httpResponse,
                TestErrorDefinition,
                null,
                CancellationToken.None);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(TestErrorDefinition.code, exception.HResult);
        }

        [Fact]
        public void CreateErrorResponseException_WithMultipleHeaderValues_JoinsCorrectly()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
            httpResponse.Headers.Add("X-Custom-Header", new[] { "value1", "value2", "value3" });

            // Act
            var exception = ErrorResponseException.CreateErrorResponseException(
                httpResponse,
                TestErrorDefinition,
                null,
                CancellationToken.None);

            // Assert
            Assert.True(exception.Data.Contains("X-Custom-Header"));
            Assert.Equal("value1,value2,value3", exception.Data["X-Custom-Header"]);
        }

        [Fact]
        public void CreateErrorResponseException_WithoutHttpResponse_CreatesBasicException()
        {
            // Act
            var exception = ErrorResponseException.CreateErrorResponseException(
                TestErrorDefinition,
                null);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(TestErrorDefinition.code, exception.HResult);
            Assert.Equal(TestErrorDefinition.description, exception.Message);
            Assert.Equal(TestErrorDefinition.helplink, exception.HelpLink);
            Assert.Null(exception.StatusCode);
        }

        [Fact]
        public void CreateErrorResponseException_WithErrorParameters_FormatsMessage()
        {
            // Arrange
            var errorDef = new AgentErrorDefinition(
                code: 999,
                description: "Error: {0} - {1}",
                helplink: "https://test.com"
            );

            // Act
            var exception = ErrorResponseException.CreateErrorResponseException(
                errorDef,
                null,
                "param1",
                "param2");

            // Assert
            Assert.Equal("Error: param1 - param2", exception.Message);
        }
    }
}
