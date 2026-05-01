using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
#if !NETSTANDARD
        public record StartResponse : ResponseBase
#else
    public class StartResponse : ResponseBase
#endif
    {
        /// <summary>
        /// The ID of the conversation that was started.
        /// </summary>
        [JsonPropertyName("conversationId")]

#if !NETSTANDARD
        public string? ConversationId { get; init; }
#else
        public string? ConversationId { get; set; }
#endif

    }
}
