using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections
{
    /// <summary>
    /// A section that renders action outputs
    /// </summary>
    public class ActionOutputMessageSection : PromptSection
    {
        private string _OutputVariable;

        private string _HistoryVariable;

        /// <summary>
        /// Action output message section
        /// </summary>
        /// <param name="historyVariable">The variable to retrieve the conversation history.</param>
        /// <param name="outputVariable">The variable to retrieve the action outputs from.</param>
        public ActionOutputMessageSection(string historyVariable, string outputVariable = "actionOutputs") : base(-1, true, "\n", "action: ")
        {
            this._OutputVariable = outputVariable;
            this._HistoryVariable = historyVariable;
        }

        /// <inheritdoc />
        public override Task<RenderedPromptSection<List<ChatMessage>>> RenderAsMessagesAsync(ITurnContext context, ITurnState memory, IPromptFunctions<List<string>> functions, ITokenizer tokenizer, int maxTokens, CancellationToken cancellationToken = default)
        {
            List<ChatMessage> history = memory.GetValue<List<ChatMessage>>(_HistoryVariable) ?? new();
            List<ChatMessage> messages = new();

            if (history.Count >= 1)
            {
                Dictionary<string, string> actionOutputs = memory.GetValue<Dictionary<string, string>>(_OutputVariable) ?? new();
                List<ActionCall> actionCalls = history.Last().ActionCalls ?? new();

                foreach (ActionCall actionCall in actionCalls)
                {
                    string output = "";
                    if (actionOutputs.TryGetValue(actionCall.Id!, out string? actionOutput))
                    {
                        output = actionOutput;
                    }

                    ChatMessage message = new(ChatRole.Tool)
                    {
                        ActionCallId = actionCall.Id,
                        Content = output
                    };

                    messages.Add(message);
                }
            }

            return Task.FromResult(new RenderedPromptSection<List<ChatMessage>>(messages, messages.Count));
        }
    }
}
