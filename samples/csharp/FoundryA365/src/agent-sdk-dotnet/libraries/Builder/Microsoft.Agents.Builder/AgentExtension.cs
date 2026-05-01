// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using System;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Represents an opinionated base class for Agent Extensions.
    /// </summary>
    public abstract class AgentExtension : IAgentExtension
    {
#if !NETSTANDARD
        public virtual ChannelId ChannelId { get; init;}
#else
        public virtual ChannelId ChannelId { get; set; } = string.Empty;
#endif

        [Obsolete("This method is deprecated. Please use the overload that includes the 'isAgenticOnly' parameter.")]
        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null)
        {
            AddRoute(agentApplication, routeSelector, routeHandler, isInvokeRoute, false, rank, autoSignInHandlers);
        }

        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null) 
        {
            var ensureChannelMatches = new RouteSelector(async (turnContext, cancellationToken) => {
                bool isForChannel = false; 
                if (turnContext.Activity.ChannelId != null && ChannelId != null)
                {
                    if (!string.IsNullOrEmpty(ChannelId.SubChannel) && ChannelId.SubChannel.Equals("*"))
                    {
                        isForChannel = turnContext.Activity.ChannelId.IsParentChannel(ChannelId.Channel);
                    }
                    else
                        isForChannel = turnContext.Activity.ChannelId.Equals(ChannelId);
                }
                return isForChannel && await routeSelector(turnContext, cancellationToken);
            });

            agentApplication.AddRoute(ensureChannelMatches, routeHandler, isInvokeRoute, rank, autoSignInHandlers, isAgenticOnly);
        }

        public void AddRoute(AgentApplication agentApplication, Route route)
        {
            agentApplication.AddRoute(route);
        }
    }
}
