using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections
{
    internal delegate Task<string> RenderFunction(ITurnContext context, ITurnState memory, IPromptFunctions<List<string>> functions, ITokenizer tokenizer, int maxTokens, CancellationToken cancellationToken = default);

    internal enum ParamState
    {
        Text,
        Param,
        String
    }

    /// <summary>
    /// A template section that will be rendered as a message.
    ///
    /// This section type is used to render a template as a message. The template can contain
    /// parameters that will be replaced with values from memory or call functions to generate
    /// dynamic content.
    ///
    /// Template syntax:
    ///  - `{{$memoryKey}}` - Renders the value of the specified memory key.
    /// - `{{functionName}}` -Calls the specified function and renders the result.
    /// - `{{functionName arg1 arg2 ...}}` -Calls the specified function with the provided list of arguments.
    ///
    /// Function arguments are optional and separated by spaces. They can be quoted using `'`, `"`, or `\`` delimiters.
    /// </summary>
    public class TemplateSection : PromptSection
    {
        /// <summary>
        /// template to be rendered
        /// </summary>
        public readonly string template;

        /// <summary>
        /// message role
        /// </summary>
        public readonly ChatRole role;

        private List<RenderFunction> _renderers;

        /// <summary>
        /// Creates instance of `TemplateSection`
        /// </summary>
        /// <param name="template">template to render</param>
        /// <param name="role">message role</param>
        /// <param name="tokens">tokens</param>
        /// <param name="required">required</param>
        /// <param name="separator">separator</param>
        /// <param name="prefix">prefix</param>
        public TemplateSection(string template, ChatRole role, int tokens = -1, bool required = false, string separator = "\n", string prefix = "") : base(tokens, required, separator, prefix)
        {
            this.template = template;
            this.role = role;
            this._renderers = Parse(template);
        }

        /// <inheritdoc />
        public override async Task<RenderedPromptSection<List<ChatMessage>>> RenderAsMessagesAsync(ITurnContext context, ITurnState memory, IPromptFunctions<List<string>> functions, ITokenizer tokenizer, int maxTokens, CancellationToken cancellationToken = default)
        {
            List<string> rendered = this._renderers
                .Select(async r => await r(context, memory, functions, tokenizer, maxTokens))
                .Select(t => t.Result)
                .ToList();

            string text = string.Join("", rendered);
            int length = tokenizer.Encode(text).Count;
            List<ChatMessage> messages = new();

            if (length > 0)
            {
                messages.Add(new(this.role) { Content = text });
            }

            return await Task.FromResult(this.TruncateMessages(messages, tokenizer, maxTokens));
        }

        private static List<RenderFunction> Parse(string template)
        {
            List<RenderFunction> renderers = new();

            string chunk = "";
            string delimiter = "";
            ParamState state = ParamState.Text;

            for (int i = 0; i < template.Length; i++)
            {
                char c = template[i];

                switch (state)
                {
                    case ParamState.Text:
                        if (c == '{' && template[i + 1] == '{')
                        {
                            if (chunk.Length > 0)
                            {
                                renderers.Add(CreateTextRenderer(chunk));
                                chunk = "";
                            }

                            state = ParamState.Param;
                            i++;
                        }
                        else
                        {
                            chunk += c;
                        }

                        break;
                    case ParamState.Param:
                        if (c == '}' && template[i + 1] == '}')
                        {
                            if (chunk.Length > 0)
                            {
                                chunk = chunk.Trim();
                                if (chunk[0] == '$')
                                {
                                    renderers.Add(CreateVariableRenderer(chunk.Substring(1)));
                                }
                                else
                                {
                                    renderers.Add(CreateFunctionRenderer(chunk));
                                }

                                chunk = "";
                            }

                            state = ParamState.Text;
                            i++;
                        }
                        else if (c == '\'' || c == '"' || c == '`')
                        {
                            delimiter = c.ToString();
                            state = ParamState.String;
                            chunk += c;
                        }
                        else
                        {
                            chunk += c;
                        }

                        break;
                    case ParamState.String:
                        chunk += c;

                        if (c.ToString() == delimiter)
                        {
                            state = ParamState.Param;
                        }

                        break;
                }
            }

            if (state != ParamState.Text)
            {
                throw new InvalidOperationException($"Invalid Template: {template}");
            }

            if (chunk.Length > 0)
            {
                renderers.Add(CreateTextRenderer(chunk));
            }

            return renderers;
        }

        private static RenderFunction CreateTextRenderer(string text)
        {
            return (context, memory, functions, tokenizer, maxTokens, cancellationToken) =>
            {
                return Task.FromResult(text);
            };
        }

        private static RenderFunction CreateVariableRenderer(string name)
        {
            return (context, memory, functions, tokenizer, maxTokens, cancellationToken) =>
            {
                object? value = memory.GetValue<object?>(name);

                if (value == null)
                {
                    return Task.FromResult(string.Empty);
                }

                string text = string.Empty;

                try
                {
                    text = JsonSerializer.Serialize(value);
                }
                catch (Exception)
                {
                    text = Convert.ToString(value)!;
                }

                return Task.FromResult(text)!;
            };
        }

        private static RenderFunction CreateFunctionRenderer(string param)
        {
            string name = "";
            List<string> args = new();
            string chunk = "";
            string delimiter = "";
            ParamState state = ParamState.Text;

            for (int i = 0; i < param.Length; i++)
            {
                bool save = false;
                char c = param[i];

                switch (state)
                {
                    case ParamState.Text:
                        if (c == '\'' || c == '"' || c == '`' || c == ' ')
                        {
                            if (c == '\'' || c == '"' || c == '`')
                            {
                                delimiter = c.ToString();
                                state = ParamState.String;
                            }

                            save = true;
                        }

                        break;
                    case ParamState.String:
                        if (c.ToString() == delimiter)
                        {
                            save = true;
                            state = ParamState.Text;
                        }

                        break;
                }

                if (save)
                {
                    if (chunk.Length > 0)
                    {
                        if (name == "")
                        {
                            name = chunk;
                        }
                        else
                        {
                            args.Add(chunk);
                        }

                        chunk = "";
                    }
                }
                else
                {
                    chunk += c;
                }
            }

            if (chunk.Length > 0)
            {
                if (name == "")
                {
                    name = chunk;
                }
                else
                {
                    args.Add(chunk);
                }

                chunk = "";
            }


            return async (context, memory, functions, tokenizer, maxTokens, cancellationToken) =>
            {
                dynamic? value = await functions.InvokeFunctionAsync(name, context, memory, tokenizer, args, cancellationToken);
                return await Task.FromResult(Convert.ToString(value));
            };
        }
    }
}
