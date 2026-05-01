using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;

namespace Microsoft.Agents.Extensions.Teams.AI.Models
{
    /// <summary>
    /// Events emitted by a IPromptCompletionStreamingModel.
    /// </summary>
    public interface IPromptCompletionModelEvents
    {

        /// <summary>
        /// Defines the method that is triggered before the model is called to complete a prompt.
        /// </summary>
        /// <returns></returns>
        public delegate void BeforeCompletionHandler(object sender, BeforeCompletionEventArgs args);

        /// <summary>
        /// Defines the method that is triggered when a chunk is received from the model via streaming.
        /// </summary>
        /// <returns></returns>
        public delegate void ChunkReceivedHandler(object sender, ChunkReceivedEventArgs args);

        /// <summary>
        /// Defines the method that is triggered after the model finishes returning a response.
        /// </summary>
        /// <returns></returns>
        public delegate void ResponseReceivedHandler(object sender, ResponseReceivedEventArgs args);
    }

    /// <summary>
    /// Defines the arguments for a BeforeCompletion event.
    /// </summary>
    public class BeforeCompletionEventArgs : EventArgs
    {
        /// <summary>
        /// Current turn context.
        /// </summary>
        public ITurnContext TurnContext { get; set; }

        /// <summary>
        /// An interface for accessing state values.
        /// </summary>
        public ITurnState Memory { get; set; }

        /// <summary>
        /// Functions to use when rendering the prompt.
        /// </summary>
        public IPromptFunctions<List<string>> PromptFunctions { get; set; }

        /// <summary>
        /// Tokenizer to use when rendering the prompt.
        /// </summary>
        public ITokenizer Tokenizer { get; set; }

        /// <summary>
        /// Prompt template being completed.
        /// </summary>
        public PromptTemplate PromptTemplate { get; set; }

        /// <summary>
        /// Returns 'true' if the prompt response is being streamed.
        /// </summary>
        public bool Streaming { get; set; }

        /// <summary>
        /// Creates a new instance of the BeforeCompletionEventArgs.
        /// </summary>
        /// <param name="turnContext">Current turn context.</param>
        /// <param name="memory">An interface for accessing state.</param>
        /// <param name="promptFunctions">Functions to use when rendering the prompt.</param>
        /// <param name="tokenizer">Tokenizer to ue when rendering the prompt.</param>
        /// <param name="promptTemplate">Prompt template being configured.</param>
        /// <param name="streaming">Returns true if streaming is enabled.</param>
        public BeforeCompletionEventArgs(ITurnContext turnContext, ITurnState memory, IPromptFunctions<List<string>> promptFunctions, ITokenizer tokenizer, PromptTemplate promptTemplate, bool streaming)
        {
            this.TurnContext = turnContext;
            this.Memory = memory;
            this.PromptFunctions = promptFunctions;
            this.Tokenizer = tokenizer;
            this.PromptTemplate = promptTemplate;
            this.Streaming = streaming;
        }
    }

    /// <summary>
    /// Defines the arguments for a ChunkReceived event.
    /// </summary>
    public class ChunkReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Current turn context.
        /// </summary>
        public ITurnContext TurnContext { get; set; }

        /// <summary>
        /// An interface for accessing state values.
        /// </summary>
        public ITurnState Memory { get; set; }

        /// <summary>
        /// Message delta received from the model.
        /// </summary>
        public PromptChunk Chunk { get; set; }

        /// <summary>
        /// Creates a new instance of ChunkReceivedEventArgs.
        /// </summary>
        /// <param name="turnContext">Current turn context.</param>
        /// <param name="memory">An interface for accessing state.</param>
        /// <param name="chunk">Message delta received from the model.</param>
        public ChunkReceivedEventArgs(ITurnContext turnContext, ITurnState memory, PromptChunk chunk)
        {
            this.TurnContext = turnContext;
            this.Memory = memory;
            this.Chunk = chunk;
        }
    }

    /// <summary>
    /// Defines the arguments for a ResponseReceived event.
    /// </summary>
    public class ResponseReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Current turn context.
        /// </summary>
        public ITurnContext TurnContext { get; set; }

        /// <summary>
        /// An interface for accessing state values.
        /// </summary>
        public ITurnState Memory { get; set; }

        /// <summary>
        /// Final response returned by the model.
        /// </summary>
        public PromptResponse Response { get; set; }

        /// <summary>
        /// Streamer object instance.
        /// </summary>
        public IStreamingResponse Streamer { get; set; }

        /// <summary>
        /// Creates a new instance of ResponseReceivedEventArgs.
        /// </summary>
        /// <param name="turnContext">Current turn context.</param>
        /// <param name="memory">An interface for accessing state.</param>
        /// <param name="response">Response returned by the model.</param>
        /// <param name="streamer">Streamer instance.</param>
        public ResponseReceivedEventArgs(ITurnContext turnContext, ITurnState memory, PromptResponse response, IStreamingResponse streamer)
        {
            this.TurnContext = turnContext;
            this.Memory = memory;
            this.Response = response;
            this.Streamer = streamer;
        }
    }
}
