using Microsoft.Agents.Core.Models;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
#if !NETSTANDARD
    public abstract record ResponseBase
#else
    public class ResponseBase
#endif
    {
        /// <summary>
        /// The activities that should be shown on the client.
        /// </summary>
        [JsonPropertyName("activities")]
#if !NETSTANDARD
        public Activity[] Activities { get; init; } = [];
#else
        public Activity[] Activities { get; set; } = [];
#endif
    }
}
