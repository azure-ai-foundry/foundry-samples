
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;

namespace Microsoft.Agents.Builder.TestBot.Shared.Debugging
{
    public class DebugAdapter : CloudAdapter
    {
        public DebugAdapter(IChannelServiceClientFactory channelServiceClientFactory, IActivityTaskQueue activityTaskQueue) : base(channelServiceClientFactory, activityTaskQueue, options: new AdapterOptions() {  Async = false })
        {
        }
    }
}
