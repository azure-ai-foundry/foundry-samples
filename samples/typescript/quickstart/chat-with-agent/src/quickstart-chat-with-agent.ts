import { DefaultAzureCredential } from "@azure/identity";
import { AIProjectClient } from "@azure/ai-projects";
import "dotenv/config";

// Format: "https://resource_name.ai.azure.com/api/projects/project_name"
const projectEndpoint: string = process.env["PROJECT_ENDPOINT"] || "<project endpoint>";
const agentName: string = process.env["AGENT_NAME"] || "<agent name>";

async function main(): Promise<void> {
    // Create project and openai clients to call Foundry API
    const project = new AIProjectClient(projectEndpoint, new DefaultAzureCredential());
    const openai = project.getOpenAIClient();

    // Create a conversation for multi-turn chat
    const conversation = await openai.conversations.create();

    // Chat with the agent to answer questions
    const response = await openai.responses.create(
        {
            conversation: conversation.id,
            input: "What is the size of France in square miles?",
        },
        {
            body: { agent_reference: { name: agentName, type: "agent_reference" } },
        },
    );
    console.log(response.output_text);

    // Ask a follow-up question in the same conversation
    const response2 = await openai.responses.create(
        {
            conversation: conversation.id,
            input: "And what is the capital city?",
        },
        {
            body: { agent_reference: { name: agentName, type: "agent_reference" } },
        },
    );
    console.log(response2.output_text);
}

main().catch(console.error);