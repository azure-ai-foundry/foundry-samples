// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.AI.OpenAI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ClientModel;
using System.Threading;
using System.Threading.Tasks;
using WeatherAgent;
using Xunit;

namespace Microsoft.Agents.SampleTest
{
    /// <summary>
    /// Demonstrates how to test an agent that uses Semantic Kernel and produces
    /// variable (AI-generated) responses, using <see cref="AgentTestHost"/> and
    /// a custom <see cref="IResponseValidator"/> backed by Azure OpenAI.
    ///
    /// <para>
    /// This test is skipped in CI. To run it manually, set the following
    /// environment variables and remove or override the Skip string:
    /// <list type="bullet">
    ///   <item><c>AZURE_OPENAI_ENDPOINT</c> — e.g. <c>https://my-resource.openai.azure.com/</c></item>
    ///   <item><c>AZURE_OPENAI_KEY</c> — Azure OpenAI API key</item>
    ///   <item><c>AZURE_OPENAI_DEPLOYMENT_NAME</c> — deployment name, e.g. <c>gpt-4o</c></item>
    /// </list>
    /// </para>
    /// </summary>
    public class WeatherAgentTest
    {
        /// <summary>
        /// An <see cref="IResponseValidator"/> that handles both plain-text and Adaptive Card
        /// replies from the WeatherAgent sample. It extracts a string representation of the reply content,
        /// then asks an Azure OpenAI chat model a yes/no question to semantically validate it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This demonstrates how to implement <see cref="IResponseValidator"/> for agents whose
        /// responses are non-deterministic. The same Azure OpenAI deployment used by the agent
        /// under test can be reused here — it acts as a judge, not a generator.
        /// </para>
        /// <para>
        /// Content extraction rules:
        /// <list type="bullet">
        ///   <item>If <see cref="IActivity.Text"/> is non-empty, use it directly.</item>
        ///   <item>If the reply has an Adaptive Card attachment, use <c>attachment.Content.ToString()</c>.
        ///         The <c>Content</c> property is already the raw JSON string produced by the LLM —
        ///         do not re-serialize it.</item>
        ///   <item>Otherwise, throw to signal an unexpected reply shape.</item>
        /// </list>
        /// </para>
        /// </remarks>
        private sealed class WeatherResponseValidator : IResponseValidator
        {
            private readonly IChatClient _chatClient;
            private readonly string _location;

            public WeatherResponseValidator(IChatClient chatClient, string location)
            {
                _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
                _location = location ?? throw new ArgumentNullException(nameof(location));
            }

            /// <inheritdoc/>
            public async Task ValidateAsync(IActivity reply, CancellationToken cancellationToken = default)
            {
                // Extract the content to validate.
                string content;
                if (!string.IsNullOrEmpty(reply?.Text))
                {
                    content = reply.Text;
                }
                else
                {
                    var adaptiveCardAttachment = reply?.Attachments?
                        .FirstOrDefault(a => string.Equals(
                            a.ContentType,
                            ContentTypes.AdaptiveCard,
                            StringComparison.OrdinalIgnoreCase));

                    if (adaptiveCardAttachment?.Content != null)
                    {
                        // After round-tripping through TestAdapter's System.Text.Json deserialisation,
                        // Attachment.Content is a JsonElement (not a string), even though WeatherForecastAgent
                        // originally set it to a string. JsonElement.ToString() returns the raw JSON text of
                        // the element without additional encoding — do NOT call JsonSerializer.Serialize()
                        // on it, which would wrap the JSON in an extra layer of string quotes.
                        content = adaptiveCardAttachment.Content.ToString()!;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Reply contains neither text nor an Adaptive Card attachment. " +
                            $"Activity type: {reply?.Type}, attachments: {reply?.Attachments?.Count ?? 0}");
                    }
                }

                // Ask the AI model a yes/no question about the content.
                string assertionPrompt =
                    $"Does this response contain weather forecast information for {_location}?";

                var messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System,
                        "You are a test evaluator. Answer only \"yes\" or \"no\" — no other text."),
                    new ChatMessage(ChatRole.User,
                        $"Content: \"{content}\"\nQuestion: {assertionPrompt}")
                };

                var options = new ChatOptions { MaxOutputTokens = 5 };
                var completion = await _chatClient.GetResponseAsync(messages, options, cancellationToken)
                    .ConfigureAwait(false);
                var response = completion?.Text?.Trim().ToLowerInvariant() ?? string.Empty;

                if (response.StartsWith("yes", StringComparison.Ordinal))
                {
                    return; // Validation passed.
                }

                if (response.StartsWith("no", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Semantic validation failed.\n" +
                        $"Prompt: {assertionPrompt}\n" +
                        $"Agent replied: {content}");
                }

                throw new InvalidOperationException(
                    $"WeatherResponseValidator received unexpected evaluator response: '{response}'. " +
                    $"Expected 'yes' or 'no'.");
            }
        }

        // ---------------------------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Sends a weather question to <c>MyAgent</c> and validates the response using
        /// an AI-backed <see cref="IResponseValidator"/>.
        ///
        /// <para>
        /// Run manually by setting the three environment variables listed below and
        /// removing the Skip string (or overriding it per-session).
        /// </para>
        /// </summary>
        [Fact(Skip =
            "Requires Azure OpenAI. Set env vars: AZURE_OPENAI_ENDPOINT, " +
            "AZURE_OPENAI_KEY, AZURE_OPENAI_DEPLOYMENT_NAME")]
        public async Task WeatherAgent_RespondsWithWeatherForecast()
        {
            // --- Credentials ----------------------------------------------------------------
            // Read from environment — never hardcode or commit these values.
            string endpoint   = GetRequiredEnvVar("AZURE_OPENAI_ENDPOINT");
            string apiKey     = GetRequiredEnvVar("AZURE_OPENAI_KEY");
            string deployment = GetRequiredEnvVar("AZURE_OPENAI_DEPLOYMENT_NAME");

            // --- Kernel (same setup as WeatherAgent's Program.cs) ---------------------------
            // Kernel is used by MyAgent internally to power the WeatherForecastAgent.
            Kernel kernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey)
                .Build();

            // --- Validator ------------------------------------------------------------------
            // Build an IChatClient backed by the same Azure OpenAI deployment.
            // This client acts as a judge — it evaluates the agent's reply, not generates it.
            //
            // AzureOpenAIClient.GetChatClient() returns OpenAI.Chat.ChatClient.
            // AsIChatClient() is the extension method from Microsoft.Extensions.AI.OpenAI
            // (OpenAIClientExtensions) that wraps OpenAI.Chat.ChatClient as IChatClient.
            // ApiKeyCredential is from System.ClientModel (transitive dep of Azure.AI.OpenAI).
            IChatClient chatClient = new AzureOpenAIClient(
                    new Uri(endpoint),
                    new ApiKeyCredential(apiKey))
                .GetChatClient(deployment)
                .AsIChatClient();

            var validator = new WeatherResponseValidator(chatClient, location: "Seattle");

            // --- Host -----------------------------------------------------------------------
            // AgentTestHost pre-registers TestAdapter as IChannelAdapter.
            // Do NOT use AddAgent<T>() here — that also registers CloudAdapter and conflicts.
            // Register IAgent directly as transient instead.
            await using var host = AgentTestHost.Create(builder =>
            {
                builder.Services.AddSingleton<IStorage, MemoryStorage>();
                builder.Services.AddSingleton<Kernel>(kernel);

                // MyAgent(AgentApplicationOptions, Kernel) — both resolved from DI.
                builder.Services.AddTransient<IAgent>(sp =>
                    new MyAgent(
                        new AgentApplicationOptions(sp.GetRequiredService<IStorage>()),
                        sp.GetRequiredService<Kernel>()));
            });

            // --- Act / Assert ---------------------------------------------------------------
            // The WeatherForecastPlugin returns simulated random temperature data —
            // no real weather API credentials are needed.
            //
            // AI calls (SK + validator) can be slow; allow 60 seconds for the reply.
            const uint weatherReplyTimeoutMs = 60_000;

            await host.CreateTestFlow()

                // Trigger the welcome message.
                // The no-arg overload automatically adds Conversation.User to MembersAdded,
                // which is equivalent to the explicit-member overload for single-user tests.
                .SendConversationUpdate()
                .AssertReplyContains("Hello and Welcome!")

                // Ask for a weather forecast. The agent will:
                //   1. Call WeatherForecastPlugin (simulated data, random temperature).
                //   2. Format the result as an Adaptive Card (per agent instructions).
                // WeatherResponseValidator handles both text and Adaptive Card replies.
                .Send("What is the weather forecast for Seattle today?")
                .AssertReplySatisfies(validator, timeout: weatherReplyTimeoutMs)

                // Guard against accidental extra sends.
                .AssertNoMoreReplies()

                .StartTestAsync();
        }

        // ---------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Reads a required environment variable, throwing a descriptive
        /// <see cref="InvalidOperationException"/> if it is not set.
        /// </summary>
        private static string GetRequiredEnvVar(string name) =>
            Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException(
                $"Required environment variable '{name}' is not set. " +
                $"This test requires Azure OpenAI credentials to run.");
    }
}
