using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Represents a subscription event containing an activity and optional SSE event ID.
    /// </summary>
    /// <param name="Activity">The activity received from the copilot.</param>
    /// <param name="EventId">The SSE event ID for resumption (null for JSON responses).</param>
#if !NETSTANDARD
    public record SubscribeEvent(IActivity Activity, string? EventId);
#else
    public class SubscribeEvent(IActivity Activity, string? EventId)
    {
      public IActivity Activity { get; set; } = Activity;
      public string? EventId { get; set; } = EventId;
    }
#endif
}
