package com.azure.ai.foundry.samples;

import com.azure.ai.foundry.samples.utils.ConfigLoader;
import com.azure.ai.projects.AIProjectClient;
import com.azure.ai.projects.AIProjectClientBuilder;
import com.azure.identity.DefaultAzureCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;
import com.openai.client.OpenAIClient;
import com.openai.client.completions.ChatCompletionRequest;
import com.openai.client.completions.ChatCompletionResponse;
import com.openai.client.completions.ChatMessage;

import java.util.Arrays;
import java.util.List;

/**
 * Demonstrates how to run a basic chat completion using the Azure AI Foundry SDK.
 */
public class ChatCompletionSample {
    public static void main(String[] args) {
        // Load environment settings
        var endpoint = ConfigLoader.getAzureEndpoint();
        var deploymentName = ConfigLoader.getAzureDeployment();

        // Authenticate with DefaultAzureCredential
        var credential = new DefaultAzureCredentialBuilder().build();

        // Build the project client
        var project = new AIProjectClientBuilder()
            .endpoint(endpoint)
            .credential(credential)
            .buildClient();

        // Acquire an OpenAI client for chat completions
        var openAI = project.inference().getAzureOpenAIClient("2024-10-21");

        // Compose the conversation
        var messages = List.of(
            new ChatMessage("system", "You are a helpful assistant."),
            new ChatMessage("user", "Tell me about Azure AI Foundry.")
        );

        var request = ChatCompletionRequest.builder()
            .model(deploymentName)
            .messages(messages)
            .build();

        // Send the request
        var response = openAI.createChatCompletion(request);

        // Display the result
        System.out.println(response.getChoices().get(0).getMessage().getContent());
    }
}
