package com.azure.ai.agents;

import com.azure.ai.agents.models.AgentDetails;
import com.azure.ai.agents.models.AgentReference;
import com.azure.ai.agents.models.AgentVersionDetails;
import com.azure.ai.agents.models.PromptAgentDefinition;
import com.azure.identity.AuthenticationUtil;
import com.azure.identity.DefaultAzureCredentialBuilder;
import com.openai.azure.AzureOpenAIServiceVersion;
import com.openai.azure.AzureUrlPathMode;
import com.openai.client.OpenAIClient;
import com.openai.client.okhttp.OpenAIOkHttpClient;
import com.openai.credential.BearerTokenCredential;
import com.openai.models.conversations.Conversation;
import com.openai.models.conversations.items.ItemCreateParams;
import com.openai.models.responses.EasyInputMessage;
import com.openai.models.responses.Response;
import com.openai.models.responses.ResponseCreateParams;

public class ChatWithAgent {
    public static void main(String[] args) {
        // Format: "https://resource_name.ai.azure.com/api/projects/project_name"
        String ProjectEndpoint = "your_project_endpoint";
        String AgentName = "your_agent_name";
        
        AgentsClient agentsClient = new AgentsClientBuilder()
                .credential(new DefaultAzureCredentialBuilder().build())
                .endpoint(ProjectEndpoint)
                .buildAgentsClient();

        AgentDetails agent = agentsClient.getAgent(AgentName);

        Conversation conversation = conversationsClient.getConversationService().create();
        conversationsClient.getConversationService().items().create(
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
        Response response = responsesClient.createWithAgentConversation(agentReference, conversation.id());

        OpenAIClient client = OpenAIOkHttpClient.builder()
            .baseUrl(ProjectEndpoint.endsWith("/") ? ProjectEndpoint + "openai" : ProjectEndpoint + "/openai")
            .azureUrlPathMode(AzureUrlPathMode.UNIFIED)
            .credential(BearerTokenCredential.create(AuthenticationUtil.getBearerTokenSupplier(
                    new DefaultAzureCredentialBuilder().build(), "https://ai.azure.com/.default")))
            .azureServiceVersion(AzureOpenAIServiceVersion.fromString("2025-11-15-preview"))
            .build();

        ResponseCreateParams responseRequest = new ResponseCreateParams.Builder()
            .input("Hello, how can you help me?")
            .model("gpt-5-mini") //supports all Foundry direct models
            .build();

        Response result = client.responses().create(responseRequest);
    }
}