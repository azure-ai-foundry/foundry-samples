using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Extensions.Teams.AI;

namespace Microsoft.Agents.Extensions.Teams.AI
{
    /// <summary>
    /// Options for the <see cref="Microsoft.Agents.Extensions.Teams.AI.TeamsApplication"/> class.
    /// </summary>
    public class TeamsAgentApplicationOptions : AgentApplicationOptions
    {
        public TeamsAgentApplicationOptions(IStorage storage) : base(storage)
        {
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// Optional. Application ID of the bot.
        /// </summary>
        /// <remarks>
        /// If using the <see cref="Microsoft.Agents.Builder.App.AgentApplicationOptions.LongRunningMessages"/> option, calling the <see cref="Microsoft.Agents.Builder.ChannelAdapter.ContinueConversationAsync(string, Microsoft.Agents.Core.Models.IActivity, Microsoft.Agents.Builder.AgentCallbackHandler, System.Threading.CancellationToken)"/> method, or configuring user authentication, this property is required.
        /// </remarks>
        public string? BotAppId { get; set; }

        /// <summary>
        /// The IStorage to use.
        /// </summary>
        public IStorage? Storage { get; set; }

        /// <summary>
        /// Optional. If true, the bot supports long running messages that can take longer then the 10 - 15
        /// second timeout imposed by most channels. Defaults to false.
        /// </summary>
        /// <remarks>
        /// This works by immediately converting the incoming request to a proactive conversation. Care should
        /// be used for bots that operate in a shared hosting environment. The incoming request is immediately
        /// completed and many shared hosting environments will mark the bot's process as idle and shut it down.
        /// </remarks>
        public bool LongRunningMessages { get; set; } = false;

        public AIOptions? AI { get; set; } = null;
    }
}
