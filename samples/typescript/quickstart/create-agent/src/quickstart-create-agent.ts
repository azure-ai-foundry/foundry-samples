import { DefaultAzureCredential } from "@azure/identity";
import { AIProjectClient } from "@azure/ai-projects";
import "dotenv/config";

// Format: "https://resource_name.ai.azure.com/api/projects/project_name"
const projectEndpoint: string = process.env["PROJECT_ENDPOINT"] || "<project endpoint>";
const modelDeploymentName: string = process.env["MODEL_DEPLOYMENT_NAME"] || "<model deployment name>";
const agentName: string = process.env["AGENT_NAME"] || "<agent name>";

async function main(): Promise<void> {
    // Create project client to call Foundry API
    const project = new AIProjectClient(projectEndpoint, new DefaultAzureCredential());

    // Create an agent with a model and instructions
    const agent = await project.agents.createVersion(agentName, {
        kind: "prompt",
        model: modelDeploymentName,
        instructions: "You are a helpful assistant that answers general questions",
    });
    console.log(`Agent created (id: ${agent.id}, name: ${agent.name}, version: ${agent.version})`);
}

main().catch(console.error);