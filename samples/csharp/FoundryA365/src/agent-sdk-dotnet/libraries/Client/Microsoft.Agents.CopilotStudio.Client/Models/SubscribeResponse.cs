namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Response model for subscribe operations.
    /// </summary>
#if !NETSTANDARD
    public record SubscribeResponse : ResponseBase
#else
    public class SubscribeResponse : ResponseBase
#endif
    {
    }
}
