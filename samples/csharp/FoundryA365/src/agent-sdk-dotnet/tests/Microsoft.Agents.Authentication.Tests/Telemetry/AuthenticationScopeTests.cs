// Copyright (c) Microsoft Corporation. All rights reserved.
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Microsoft.Agents.Authentication.Telemetry.Scopes;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.Agents.TestSupport;
using Xunit;

namespace Microsoft.Agents.Authentication.Tests.Telemetry
{

    [Collection("TelemetryTests")]
    public class AuthenticationScopeTests : TelemetryScopeTestBase
    {

        #region ScopeGetAccessToken

        [Fact]
        public void ScopeGetAccessToken_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetAccessToken(new[] { "scope1" }, "obo");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.authentication.get_access_token", started.OperationName);
        }

        [Fact]
        public void ScopeGetAccessToken_Callback_SetsAuthMethodTag()
        {
            var scope = new ScopeGetAccessToken(new[] { "scope1" }, "obo");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("obo", stopped.GetTagItem(TagNames.AuthMethod));
        }

        [Fact]
        public void ScopeGetAccessToken_Callback_SetsAuthScopesTag()
        {
            var scope = new ScopeGetAccessToken(new[] { "https://api.botframework.com/.default", "openid" }, "obo");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("https://api.botframework.com/.default,openid", stopped.GetTagItem(TagNames.AuthScopes));
        }

        [Fact]
        public void ScopeGetAccessToken_Callback_SetsUnknownScopes_WhenEmpty()
        {
            var scope = new ScopeGetAccessToken(Array.Empty<string>(), "obo");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(TelemetryUtils.Unknown, stopped.GetTagItem(TagNames.AuthScopes));
        }

        [Fact]
        public void ScopeGetAccessToken_SetError_SetsErrorStatus()
        {
            var scope = new ScopeGetAccessToken(new[] { "scope1" }, "obo");
            scope.SetError(new InvalidOperationException("token failed"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("token failed", stopped.StatusDescription);
        }

        #endregion

        #region ScopeAcquireTokenOnBehalfOf

        [Fact]
        public void ScopeAcquireTokenOnBehalfOf_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeAcquireTokenOnBehalfOf(new[] { "scope1" });

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.authentication.acquire_token_on_behalf_of", started.OperationName);
        }

        [Fact]
        public void ScopeAcquireTokenOnBehalfOf_Callback_SetsAuthMethodTag()
        {
            var scope = new ScopeAcquireTokenOnBehalfOf(new[] { "scope1" });
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("obo", stopped.GetTagItem(TagNames.AuthMethod));
        }

        [Fact]
        public void ScopeAcquireTokenOnBehalfOf_Callback_SetsAuthScopesTag()
        {
            var scope = new ScopeAcquireTokenOnBehalfOf(new[] { "scope-a", "scope-b" });
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("scope-a,scope-b", stopped.GetTagItem(TagNames.AuthScopes));
        }

        [Fact]
        public void ScopeAcquireTokenOnBehalfOf_Callback_SetsUnknownScopes_WhenEmpty()
        {
            var scope = new ScopeAcquireTokenOnBehalfOf(Array.Empty<string>());
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(TelemetryUtils.Unknown, stopped.GetTagItem(TagNames.AuthScopes));
        }

        [Fact]
        public void ScopeAcquireTokenOnBehalfOf_SetError_SetsErrorStatus()
        {
            var scope = new ScopeAcquireTokenOnBehalfOf(new[] { "scope1" });
            scope.SetError(new UnauthorizedAccessException("obo failed"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeGetAgenticInstanceToken

        [Fact]
        public void ScopeGetAgenticInstanceToken_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetAgenticInstanceToken("instance-123");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.authentication.get_agentic_instance_token", started.OperationName);
        }

        [Fact]
        public void ScopeGetAgenticInstanceToken_Callback_SetsAuthMethodTag()
        {
            var scope = new ScopeGetAgenticInstanceToken("instance-123");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("agentic_instance", stopped.GetTagItem(TagNames.AuthMethod));
        }

        [Fact]
        public void ScopeGetAgenticInstanceToken_Callback_SetsAgenticInstanceIdTag()
        {
            var scope = new ScopeGetAgenticInstanceToken("instance-abc");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("instance-abc", stopped.GetTagItem(TagNames.AgenticInstanceId));
        }

        [Fact]
        public void ScopeGetAgenticInstanceToken_SetError_SetsErrorStatus()
        {
            var scope = new ScopeGetAgenticInstanceToken("instance-123");
            scope.SetError(new InvalidOperationException("instance token failed"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeGetAgenticUserToken

        [Fact]
        public void ScopeGetAgenticUserToken_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetAgenticUserToken("instance-1", "user-1", null);

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.authentication.get_agentic_user_token", started.OperationName);
        }

        [Fact]
        public void ScopeGetAgenticUserToken_Callback_SetsAuthMethodTag()
        {
            var scope = new ScopeGetAgenticUserToken("instance-1", "user-1", null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("agentic_user", stopped.GetTagItem(TagNames.AuthMethod));
        }

        [Fact]
        public void ScopeGetAgenticUserToken_Callback_SetsInstanceAndUserIdTags()
        {
            var scope = new ScopeGetAgenticUserToken("instance-42", "user-99", null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("instance-42", stopped.GetTagItem(TagNames.AgenticInstanceId));
            Assert.Equal("user-99", stopped.GetTagItem(TagNames.AgenticUserId));
        }

        [Fact]
        public void ScopeGetAgenticUserToken_Callback_SetsAuthScopesTag_WhenProvided()
        {
            var scope = new ScopeGetAgenticUserToken("instance-1", "user-1", new[] { "scope-x", "scope-y" });
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("scope-x,scope-y", stopped.GetTagItem(TagNames.AuthScopes));
        }

        [Fact]
        public void ScopeGetAgenticUserToken_Callback_SetsUnknownScopes_WhenNull()
        {
            var scope = new ScopeGetAgenticUserToken("instance-1", "user-1", null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(TelemetryUtils.Unknown, stopped.GetTagItem(TagNames.AuthScopes));
        }

        [Fact]
        public void ScopeGetAgenticUserToken_SetError_SetsErrorStatus()
        {
            var scope = new ScopeGetAgenticUserToken("instance-1", "user-1", null);
            scope.SetError(new InvalidOperationException("user token failed"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
        }

        #endregion
    }
}