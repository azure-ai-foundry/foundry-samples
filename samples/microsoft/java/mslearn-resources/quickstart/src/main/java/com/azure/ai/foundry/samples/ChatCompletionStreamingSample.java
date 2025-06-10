package com.azure.ai.foundry.samples;

import com.azure.ai.foundry.samples.utils.ConfigLoader;
import com.azure.ai.projects.AIProjectClient;
import com.azure.ai.projects.AIProjectClientBuilder;
import com.azure.identity.DefaultAzureCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;
import com.openai.client.OpenAIClient;
import com.openai.client.completions.ChatCompletionRequest;
import com.openai.client.completions.ChatMessage;
import com.openai.client.completions.ChatCompletionStreamResponse;

import java.util.Arrays;
import java.util.List;

/**
 * Demonstrates streaming chat completions with Azure AI Foundry.
 */
public class ChatCompletionStreamingSample {
    public static void main(String[] args) {
        var endpoint = ConfigLoader.getAzureEndpoint();
        var deploymentName = ConfigLoader.getAzureDeployment();

        var credential = new DefaultAzureCredentialBuilder().build();

        var project = new AIProjectClientBuilder()
            .endpoint(endpoint)
            .credential(credential)
            .buildClient();

        var openAI = project.inference().getAzureOpenAIClient("2024-10-21");

        var messages = List.of(
            new ChatMessage("system", "You are a helpful assistant."),
            new ChatMessage("user", "Write a short poem about cloud computing.")
        );

        var request = ChatCompletionRequest.builder()
            .model(deploymentName)
            .messages(messages)
            .stream(true)
            .build();

        System.out.println("Response from assistant (streaming):");
        openAI.streamChatCompletion(request, (ChatCompletionStreamResponse delta) -> {
            if (delta.getChoices() != null && !delta.getChoices().isEmpty()) {
                String content = delta.getChoices().get(0).getDelta().getContent();
                if (content != null) {
                    System.out.print(content);
                }
            }
        });
        System.out.println("\nStreaming completed!");
    }
}
