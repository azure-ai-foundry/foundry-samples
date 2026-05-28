import { DefaultAzureCredential } from "@azure/identity";
import { AIProjectClient } from "@azure/ai-projects";
import "dotenv/config";

// Format: "https://resource_name.ai.azure.com/api/projects/project_name"
const projectEndpoint: string = process.env["PROJECT_ENDPOINT"] || "<project endpoint>";
const modelDeploymentName: string = process.env["MODEL_DEPLOYMENT_NAME"] || "<model deployment name>";

async function main(): Promise<void> {
    // Create project and openai clients to call Foundry API
    const project = new AIProjectClient(projectEndpoint, new DefaultAzureCredential());
    const openai = project.getOpenAIClient();

    // Run a responses API call
    const response = await openai.responses.create({
        model: modelDeploymentName,
        input: "What is the size of France in square miles?",
    });
    console.log(`Response output: ${response.output_text}`);
}

main().catch(console.error);