using Azure.AI.OpenAI.Chat;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.AI.Utilities;

namespace Microsoft.Agents.Extensions.Teams.AI.Models
{
    /// <summary>
    /// The message context.
    /// </summary>
    public class MessageContext
    {
        /// <summary>
        /// Citations used in the message.
        /// </summary>
        public IList<Citation> Citations { get; set; } = new List<Citation>();

        /// <summary>
        /// The intent of the message.
        /// </summary>
        public string Intent { get; set; } = string.Empty;

        /// <summary>
        /// Creates a MessageContext
        /// </summary>
        public MessageContext() { }

        /// <summary>
        /// Creates a MessageContext using OpenAI.Chat.AzureChatMessageContext.
        /// </summary>
        /// <param name="azureContext"></param>
#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        internal MessageContext(ChatMessageContext azureContext)
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        {
            if (azureContext.Citations != null)
            {
#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                foreach (ChatCitation citation in azureContext.Citations)
                {
                    this.Citations.Add(new Citation(citation.Content, citation.Title, citation.Url.ToString()));
                }
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            }

            this.Intent = azureContext.Intent;
        }
    }
}
