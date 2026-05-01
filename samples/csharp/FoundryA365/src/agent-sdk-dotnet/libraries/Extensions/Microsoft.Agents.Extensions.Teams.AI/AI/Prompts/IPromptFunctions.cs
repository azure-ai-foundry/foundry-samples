using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Prompts
{
    /// <summary>
    /// Prompt Function
    /// </summary>
    /// <param name="context">context</param>
    /// <param name="memory">memory</param>
    /// <param name="functions">functions</param>
    /// <param name="tokenizer">tokenizer</param>
    /// <param name="args">args</param>
    /// <returns></returns>
    public delegate Task<dynamic> PromptFunction<TArgs>(ITurnContext context, ITurnState memory, IPromptFunctions<TArgs> functions, ITokenizer tokenizer, TArgs args);

    /// <summary>
    /// Prompt Functions
    /// </summary>
    public interface IPromptFunctions<TArgs>
    {
        /// <summary>
        /// Check If Has Function
        /// </summary>
        /// <param name="name">function name</param>
        /// <returns>true if has function, otherwise false</returns>
        public bool HasFunction(string name);

        /// <summary>
        /// Get Function
        /// </summary>
        /// <param name="name">function name</param>
        /// <returns>the function if it exists, otherwise null</returns>
        public PromptFunction<TArgs>? GetFunction(string name);

        /// <summary>
        /// Add A Function
        /// </summary>
        /// <param name="name">function name</param>
        /// <param name="func">function to add</param>
        public void AddFunction(string name, PromptFunction<TArgs> func);

        /// <summary>
        /// Invoke A Function
        /// </summary>
        /// <param name="name">function name</param>
        /// <param name="context">context</param>
        /// <param name="memory">memory</param>
        /// <param name="tokenizer">tokenizer</param>
        /// <param name="args">args</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns></returns>
        public Task<dynamic?> InvokeFunctionAsync(string name, ITurnContext context, ITurnState memory, ITokenizer tokenizer, TArgs args, CancellationToken cancellationToken = default);
    }
}
