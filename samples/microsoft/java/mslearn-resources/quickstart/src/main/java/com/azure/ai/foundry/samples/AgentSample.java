package com.azure.ai.foundry.samples;

import com.azure.ai.foundry.samples.utils.ConfigLoader;
import com.azure.ai.projects.AIProjectClient;
import com.azure.ai.projects.AIProjectClientBuilder;
import com.azure.ai.projects.models.Agent;
import com.azure.ai.projects.models.AgentMessage;
import com.azure.ai.projects.models.AgentOptions;
import com.azure.ai.projects.models.AgentRun;
import com.azure.ai.projects.models.AgentRunStatus;
import com.azure.ai.projects.models.AgentThread;
import com.azure.identity.DefaultAzureCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;

import java.util.List;

/**
 * Demonstrates how to create and run an agent using the new AIProjectClient API.
 */
public class AgentSample {
    public static void main(String[] args) {
        var endpoint = ConfigLoader.getAzureEndpoint();
        var deploymentName = ConfigLoader.getAzureDeployment();

        var credential = new DefaultAzureCredentialBuilder().build();

        var project = new AIProjectClientBuilder()
            .endpoint(endpoint)
            .credential(credential)
            .buildClient();

        System.out.println("Creating agent...");
        var agent = project.agents().createAgent(new AgentOptions()
            .setModel(deploymentName)
            .setName("Research Assistant")
            .setInstructions("You are a research assistant. Help users find information and summarize content."));
        System.out.println("Agent created: " + agent.getId());

        var thread = project.agents().threads().create();
        System.out.println("Thread created: " + thread.getId());

        var userMessage = new AgentMessage()
            .setRole("user")
            .setContent("Explain what cloud computing is and list three benefits.");

        var run = project.agents().runs().createAndProcess(thread.getId(), agent.getId(), userMessage);
        var completedRun = waitForRunCompletion(project, thread.getId(), run.getId());
        System.out.println("Run completed with status: " + completedRun.getStatus());

        var messages = project.agents().messages().list(thread.getId());
        for (var message : messages) {
            System.out.println(message.getRole() + ": " + message.getContent());
        }
    }

    private static AgentRun waitForRunCompletion(AIProjectClient project, String threadId, String runId) {
        var run = project.agents().runs().get(threadId, runId);
        while (run.getStatus() == AgentRunStatus.QUEUED || run.getStatus() == AgentRunStatus.IN_PROGRESS) {
            try {
                Thread.sleep(1000);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
                throw new RuntimeException("Thread interrupted", e);
            }
            run = project.agents().runs().get(threadId, runId);
        }
        return run;
    }
}
