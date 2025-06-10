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
import com.azure.ai.projects.models.FileSearchTool;
import com.azure.ai.projects.models.FileResource;
import com.azure.identity.DefaultAzureCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;

/**
 * Demonstrates how to create an agent with file search capability.
 */
public class FileSearchAgentSample {
    public static void main(String[] args) {
        var endpoint = ConfigLoader.getAzureEndpoint();
        var deploymentName = ConfigLoader.getAzureDeployment();

        var credential = new DefaultAzureCredentialBuilder().build();

        var project = new AIProjectClientBuilder()
            .endpoint(endpoint)
            .credential(credential)
            .buildClient();

        try {
            var tempFile = createSampleDocument();

            var file = project.agents().files().upload(tempFile.toString());
            var vectorStore = project.agents().vectorStores().createAndPoll(List.of(file.getId()), "my_vectorstore");

            var searchTool = new FileSearchTool(vectorStore.getId());

            var agent = project.agents().createAgent(new AgentOptions()
                .setModel(deploymentName)
                .setName("Document Assistant")
                .setInstructions("You are a document assistant. Help users find information in their documents.")
                .setTools(searchTool.getDefinitions())
                .setToolResources(searchTool.getResources()));

            var thread = project.agents().threads().create();
            var userMessage = new AgentMessage()
                .setRole("user")
                .setContent("Find and list the benefits of cloud computing from my document.");

            var run = project.agents().runs().createAndProcess(thread.getId(), agent.getId(), userMessage);
            var completedRun = waitForRunCompletion(project, thread.getId(), run.getId());
            System.out.println("Run completed with status: " + completedRun.getStatus());

            var messages = project.agents().messages().list(thread.getId());
            for (var message : messages) {
                System.out.println(message.getRole() + ": " + message.getContent());
            }

            Files.deleteIfExists(tempFile);
        } catch (IOException e) {
            System.err.println("Error working with files: " + e.getMessage());
        }
    }

    private static Path createSampleDocument() throws IOException {
        var content = "# Cloud Computing Overview\n\n" +
            "Cloud computing is the delivery of computing services—including servers, storage, databases, networking, software, analytics, and intelligence—over the Internet (the cloud) to offer faster innovation, flexible resources, and economies of scale.\n\n" +
            "## Benefits of Cloud Computing\n\n" +
            "1. **Cost Savings**: Cloud computing eliminates the capital expense of buying hardware and software and setting up and running on-site data centers.\n\n" +
            "2. **Scalability**: Cloud services can be scaled up or down based on demand, providing businesses with flexibility as their needs change.\n\n" +
            "3. **Performance**: The biggest cloud computing services run on a worldwide network of secure data centers, which are regularly upgraded to the latest generation of fast and efficient computing hardware.\n\n" +
            "4. **Reliability**: Cloud computing makes data backup, disaster recovery, and business continuity easier and less expensive because data can be mirrored at multiple redundant sites on the cloud provider's network.\n\n" +
            "5. **Security**: Many cloud providers offer a broad set of policies, technologies, and controls that strengthen your security posture overall.";

        var tempFile = Files.createTempFile("cloud-computing-info-", ".md");
        Files.writeString(tempFile, content);
        return tempFile;
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
