package com.azure.ai.agents;

import com.azure.ai.agents.models.AgentDetails;
import com.azure.ai.agents.models.AgentReference;
import com.azure.ai.agents.models.AzureCreateResponseOptions;
import com.azure.ai.agents.models.AgentVersionDetails;
import com.azure.ai.agents.models.PromptAgentDefinition;
import com.azure.identity.AuthenticationUtil;
import com.azure.identity.DefaultAzureCredentialBuilder;
import com.openai.client.OpenAIClient;
import com.openai.client.okhttp.OpenAIOkHttpClient;
import com.openai.credential.BearerTokenCredential;
import com.openai.models.conversations.Conversation;
import com.openai.models.conversations.items.ItemCreateParams;
import com.openai.models.responses.EasyInputMessage;
import com.openai.models.responses.Response;
import com.openai.models.responses.ResponseCreateParams;
import com.openai.services.blocking.ConversationService;

public class ChatWithAgent {
    public static void main(String[] args) {
        // Format: "https://resource_name.ai.azure.com/api/projects/project_name"
        String ProjectEndpoint = "your_project_endpoint";
        String AgentName = "your_agent_name";
        
        AgentsClientBuilder builder = new AgentsClientBuilder()
                .credential(new DefaultAzureCredentialBuilder().build())
                .endpoint(ProjectEndpoint);

        AgentsClient agentsClient = builder.buildAgentsClient();
        ResponsesClient responsesClient = builder.buildResponsesClient();
        ConversationService conversationService
            = builder.buildOpenAIClient().conversations();

        AgentDetails agent = agentsClient.getAgent(AgentName);

        Conversation conversation = conversationService.create();
        conversationService.items().create(
            ItemCreateParams.builder()
                .conversationId(conversation.id())
                .addItem(EasyInputMessage.builder()
                    .role(EasyInputMessage.Role.SYSTEM)
                    .content("You are a helpful assistant that speaks like a pirate.")
                    .build()
                ).addItem(EasyInputMessage.builder()
                    .role(EasyInputMessage.Role.USER)
                    .content("Hello, agent!")
                    .build()
            ).build()
        );

        AgentReference agentReference = new AgentReference(agent.getName()).setVersion(agent.getVersion());
        Response response = responsesClient.createAzureResponse(
            new AzureCreateResponseOptions().setAgentReference(agentReference),
            ResponseCreateParams.builder().conversation(conversation.id()));

        OpenAIClient client = OpenAIOkHttpClient.builder()
            .baseUrl(ProjectEndpoint.endsWith("/") ? ProjectEndpoint + "openai/v1" : ProjectEndpoint + "/openai/v1")
            .credential(BearerTokenCredential.create(AuthenticationUtil.getBearerTokenSupplier(
                    new DefaultAzureCredentialBuilder().build(), "https://ai.azure.com/.default")))
            .build();

        ResponseCreateParams responseRequest = new ResponseCreateParams.Builder()
            .input("Hello, how can you help me?")
            .model("gpt-5-mini") //supports all Foundry direct models
            .build();

        Response result = client.responses().create(responseRequest);
    }
}