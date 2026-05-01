using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    internal class NotImplementedAdapter : ChannelAdapter
    {
        public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
