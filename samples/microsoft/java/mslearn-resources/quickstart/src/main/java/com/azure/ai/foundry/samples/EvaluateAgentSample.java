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
import com.azure.ai.projects.models.evaluations.AgentEvaluation;
import com.azure.ai.projects.models.evaluations.EvaluationMetric;
import com.azure.ai.projects.models.evaluations.EvaluationOptions;
import com.azure.identity.DefaultAzureCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;

import java.util.Map;

/**
 * Example of evaluating an agent run using the new beta SDK.
 */
public class EvaluateAgentSample {
    public static void main(String[] args) {
        var endpoint = ConfigLoader.getAzureEndpoint();
        var deploymentName = ConfigLoader.getAzureDeployment();

        var credential = new DefaultAzureCredentialBuilder().build();

        var project = new AIProjectClientBuilder()
            .endpoint(endpoint)
            .credential(credential)
            .buildClient();

        // Create an agent and run it to obtain a run for evaluation
        var agent = project.agents().createAgent(new AgentOptions()
            .setModel(deploymentName)
            .setName("Weather Assistant")
            .setInstructions("You are a weather assistant. Provide accurate and helpful information about weather conditions."));

        var thread = project.agents().threads().create();
        var userMessage = new AgentMessage()
            .setRole("user")
            .setContent("What should I wear if it's going to be rainy and cold tomorrow?");

        var run = project.agents().runs().createAndProcess(thread.getId(), agent.getId(), userMessage);
        var completedRun = waitForRunCompletion(project, thread.getId(), run.getId());

        // Evaluate the run
        var options = new EvaluationOptions()
            .addMetric(EvaluationMetric.HELPFULNESS)
            .addMetric(EvaluationMetric.ACCURACY)
            .addMetric(EvaluationMetric.QUALITY);

        var evaluation = project.evaluations().evaluateAgentRun(completedRun.getId(), options);

        System.out.println("Evaluation ID: " + evaluation.getId());
        for (var score : evaluation.getScores().entrySet()) {
            System.out.printf("%s Score: %.2f\n", score.getKey(), score.getValue());
        }
        System.out.println("Feedback: " + evaluation.getFeedback());
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
