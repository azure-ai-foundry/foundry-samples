package com.azure.ai.foundry.samples;

import com.azure.ai.agents.AgentsClient;
import com.azure.ai.agents.AgentsClientBuilder;
import com.azure.ai.agents.ResponsesClient;
import com.azure.ai.agents.models.AgentDetails;
import com.azure.ai.agents.models.AgentVersionDetails;
import com.azure.ai.agents.models.PromptAgentDefinition;
import com.azure.core.credential.TokenCredential;
import com.azure.core.exception.HttpResponseException;
import com.azure.core.util.logging.ClientLogger;
import com.azure.identity.DefaultAzureCredentialBuilder;
import com.openai.client.OpenAIClient;
import com.openai.models.conversations.Conversation;
import com.openai.models.responses.Response;


/**
 * Sample demonstrating how to work with Azure AI Agents using the Azure AI Agents SDK v2.
 * 
 * This sample shows how to:
 * - Set up authentication with Azure credentials
 * - Create an agent with custom instructions
 * - Start a conversation with the agent
 * - Get responses from the agent
 * - Work with the AgentsClient and ResponsesClient
 * 
 * Environment variables:
 * - AZURE_ENDPOINT: Optional fallback. The base endpoint for your Azure AI service if PROJECT_ENDPOINT is not provided.
 * - PROJECT_ENDPOINT: Required. The endpoint for your Azure AI Project.
 * - MODEL_DEPLOYMENT_NAME: Optional. The model deployment name (defaults to "gpt-4o").
 * - AGENT_NAME: Optional. The name to give to the created agent (defaults to "java-quickstart-agent").
 * - AGENT_INSTRUCTIONS: Optional. The instructions for the agent (defaults to a helpful assistant).
 * 
 * Note: This sample requires proper Azure authentication. It uses DefaultAzureCredential which supports
 * multiple authentication methods including environment variables, managed identities, and interactive login.
 * 
 * SDK Features Demonstrated:
 * - Using the Azure AI Agents SDK (com.azure:azure-ai-agents:2.0.0)
 * - Creating an authenticated client with DefaultAzureCredential
 * - Using the AgentsClientBuilder pattern for client instantiation
 * - Creating agents with specific configurations (name, model, instructions)
 * - Creating conversations and getting responses from agents
 * - Working with agent versions
 * - Accessing agent properties
 * - Implementing proper error handling for Azure service interactions
 */
public class AgentSample {
    private static final ClientLogger logger = new ClientLogger(AgentSample.class);

    public static void main(String[] args) {
        // Load environment variables with better error handling, supporting both .env and system environment variables
        String endpoint = System.getenv("AZURE_ENDPOINT");
        String projectEndpoint = System.getenv("PROJECT_ENDPOINT");
        String modelName = System.getenv("MODEL_DEPLOYMENT_NAME");
        String agentName = System.getenv("AGENT_NAME");
        String instructions = System.getenv("AGENT_INSTRUCTIONS");

        

        // Check for required endpoint configuration
        if (projectEndpoint == null && endpoint == null) {
            String errorMessage = "Environment variables not configured. Required: either PROJECT_ENDPOINT or AZURE_ENDPOINT must be set.";
            logger.error("ERROR: {}", errorMessage);
            logger.error("Please set your environment variables or create a .env file. See README.md for details.");
            return;
        }
        
        // Use AZURE_ENDPOINT as fallback if PROJECT_ENDPOINT not set
        if (projectEndpoint == null) {
            projectEndpoint = endpoint;
            logger.info("Using AZURE_ENDPOINT as PROJECT_ENDPOINT: {}", projectEndpoint);
        }

        // Set defaults for optional parameters with informative logging
        if (modelName == null) {
            modelName = "gpt-4o";
            logger.info("No MODEL_DEPLOYMENT_NAME provided, using default: {}", modelName);
        }
        if (agentName == null) {
            agentName = "java-quickstart-agent";
            logger.info("No AGENT_NAME provided, using default: {}", agentName);
        }
        if (instructions == null) {
            instructions = "You are a helpful assistant that provides clear and concise information.";
            logger.info("No AGENT_INSTRUCTIONS provided, using default instructions");
        }

        // Create Azure credential with DefaultAzureCredentialBuilder
        // This supports multiple authentication methods including environment variables,
        // managed identities, and interactive browser login
        logger.info("Building DefaultAzureCredential");
        TokenCredential credential = new DefaultAzureCredentialBuilder().build();

        try {
            // Build the agents client and related clients
            logger.info("Creating AgentsClient with endpoint: {}", projectEndpoint);
            AgentsClientBuilder builder = new AgentsClientBuilder()
                .credential(credential)
                .endpoint(projectEndpoint);

            AgentsClient agentsClient = builder.buildAgentsClient();
            ResponsesClient responsesClient = builder.buildResponsesClient();
            OpenAIClient openAIClient = builder.buildOpenAIClient();

            // Create an agent
            logger.info("Creating agent with name: {}, model: {}", agentName, modelName);
            PromptAgentDefinition agentDefinition = new PromptAgentDefinition(modelName)
                .setInstructions(instructions);
            
            AgentVersionDetails agent = agentsClient.createAgentVersion(
                agentName,
                new com.azure.ai.agents.models.CreateAgentVersionOptions(agentDefinition)
            );
            logger.info("Agent created: Name={}, Version={}", agent.getName(), agent.getVersion());
            logger.info("Agent model: {}", agent.getModel());

            // Create a conversation
            logger.info("Creating conversation with agent");
            Conversation conversation = openAIClient.conversations().create();
            logger.info("Conversation created: ID={}", conversation.id());

            // Get a response from the agent
            logger.info("Getting response from agent");
            Response response = responsesClient.createResponse(
                agentName,
                conversation.id(),
                "What are the key features of a helpful assistant?"
            );
            logger.info("Response received: {}", response.getOutputText());

            logger.info("\nDemo completed successfully!");
            
        } catch (HttpResponseException e) {
            // Handle service-specific errors with detailed information
            int statusCode = e.getResponse().getStatusCode();
            logger.error("Service error {}: {}", statusCode, e.getMessage());
            logger.error("Refer to the Azure AI Agents documentation for troubleshooting information.");
        } catch (Exception e) {
            // Handle general exceptions
            logger.error("Error in agent sample: {}", e.getMessage(), e);
        }
    }
}
