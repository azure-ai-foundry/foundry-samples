// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Compat;

namespace Microsoft.Agents.Builder.TestBot.Shared.Bots
{
    public class ProactiveBot : ActivityHandler
    {
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var claimsIdentity = turnContext.Identity as ClaimsIdentity;

            var botAppIdClaim = claimsIdentity.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim);

            var appId = botAppIdClaim.Value;

            var conversationReference = turnContext.Activity.GetConversationReference();

            await turnContext.Adapter.ContinueConversationAsync(appId, conversationReference, BotCallback, cancellationToken);
        }

        private async Task BotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync("proactive hello");
        }
    }
}
