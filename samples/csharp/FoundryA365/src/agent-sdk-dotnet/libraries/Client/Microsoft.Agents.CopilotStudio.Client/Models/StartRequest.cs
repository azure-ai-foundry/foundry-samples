using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
#if !NETSTANDARD
    public record StartRequest
#else
    public class StartRequest
#endif
    {
        /// <summary>
        /// The locale to use as defined by the client.
        /// </summary>
        [JsonPropertyName("locale")]
#if !NETSTANDARD
        public string? Locale { get; init; }
#else
        public string? Locale { get; set; }
#endif

        /// <summary>
        /// Whether to emit a StartConversation event.
        /// </summary>
        [JsonPropertyName("emitStartConversationEvent")]
#if !NETSTANDARD
        public bool EmitStartConversationEvent { get; init; }
#else
        public bool EmitStartConversationEvent { get; set; }
#endif

        /// <summary>
        /// Conversation ID requested by the client.
        /// </summary>
        [JsonPropertyName("conversationId")]
#if !NETSTANDARD
        public string? ConversationId { get; init; }
#else
        public string? ConversationId { get; set; }
#endif
    }
}
