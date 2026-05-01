// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CsvHelper;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Extensions.Configuration;

namespace EvalClient
{
    /// <summary>
    /// Connection Settings extension for the sample to include appID and TeantId for creating authentication token.
    /// </summary>
    internal class EvalClientConfig : ConnectionSettings
    {
        /// <summary>
        /// Tenant ID for creating the authentication for the connection
        /// </summary>
        public string? TenantId { get; set; }
        /// <summary>
        /// Application ID for creating the authentication for the connection
        /// </summary>
        public string? AppClientId { get; set; }
        /// <summary>
        /// Azure Open AI Endpoint
        /// </summary>
        public string? AzureOpenAiEndpoint { get; set; }
        /// <summary>
        /// Azure Open AI Key
        /// </summary>
        public string? AzureOpenAiKey { get; set; }
        /// <summary>
        /// Azure Open AI Deployment
        /// </summary>
        public string? AzureOpenAiDeployment { get; set; }
        /// <summary>
        /// Groundedness Evaluation Prompt
        /// </summary>
        public string? GroundednessEvaluationPrompt { get; set; }
        /// <summary>
        /// Groundedness Evaluation Prompt
        /// </summary>
        public string? EvaluationDataset { get; set; }

        /// <summary>
        /// Create ConnectionSettings from a configuration section.
        /// </summary>
        /// <param name="config"></param>
        /// <exception cref="System.ArgumentException"></exception>
        public EvalClientConfig(IConfigurationSection config) : base(config)
        {
            TenantId = config[nameof(TenantId)] ?? throw new ArgumentException($"{nameof(TenantId)} not found in config");
            AppClientId = config[nameof(AppClientId)] ?? throw new ArgumentException($"{nameof(AppClientId)} not found in config");
            AzureOpenAiEndpoint = config[nameof(AzureOpenAiEndpoint)] ?? throw new ArgumentException($"{nameof(AzureOpenAiEndpoint)} not found in config");
            AzureOpenAiKey = config[nameof(AzureOpenAiKey)] ?? throw new ArgumentException($"{nameof(AzureOpenAiKey)} not found in config");
            AzureOpenAiDeployment = config[nameof(AzureOpenAiDeployment)] ?? throw new ArgumentException($"{nameof(AzureOpenAiDeployment)} not found in config");
            GroundednessEvaluationPrompt = config[nameof(GroundednessEvaluationPrompt)] ?? string.Empty;
            EvaluationDataset = config[nameof(EvaluationDataset)] ?? throw new ArgumentException($"{nameof(EvaluationDataset)} not found in config");
        }
    }
}
