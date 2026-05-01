// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    public class AgenticAuthorization
    {
        private readonly IConnections _connections;

        public static bool IsAgenticRequest(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            return IsAgenticRequest(turnContext.Activity);
        }
        public static bool IsAgenticRequest(IActivity activity)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            return activity.IsAgenticRequest();
        }

        public static string GetAgenticTenantId(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));
            return turnContext.Activity.GetAgenticTenantId();
        }

        public static string GetAgenticInstanceId(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));
            return turnContext.Activity.GetAgenticInstanceId();
        }

        public static string GetAgenticUser(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));
            return turnContext.Activity.GetAgenticUser();
        }

        public AgenticAuthorization(IConnections connections)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));
            _connections = connections;
        }

        public async Task<string> GetAgenticInstanceTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (!IsAgenticRequest(turnContext))
            {
                return null;
            }

            var connection = _connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
            if (connection is IAgenticTokenProvider agenticTokenProvider)
            {
                return await agenticTokenProvider.GetAgenticInstanceTokenAsync(GetAgenticTenantId(turnContext), GetAgenticInstanceId(turnContext), cancellationToken);
            }

            throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.AgenticTokenProviderNotFound, null, $"{turnContext.Identity.GetIncomingAudience()}:{turnContext.Activity.ServiceUrl}");
        }

        public async Task<string> GetAgenticUserTokenAsync(ITurnContext turnContext, IList<string> scopes, CancellationToken cancellationToken = default)
        {
            if (!IsAgenticRequest(turnContext) || string.IsNullOrEmpty(GetAgenticUser(turnContext)))
            {
                return null;
            }

            var connection = _connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
            if (connection is IAgenticTokenProvider agenticTokenProvider)
            {
                return await agenticTokenProvider.GetAgenticUserTokenAsync(GetAgenticTenantId(turnContext), GetAgenticInstanceId(turnContext), GetAgenticUser(turnContext), scopes, cancellationToken);
            }

            throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.AgenticTokenProviderNotFound, null, $"{turnContext.Identity.GetIncomingAudience()}:{turnContext.Activity.ServiceUrl}");
        }
    }
}
