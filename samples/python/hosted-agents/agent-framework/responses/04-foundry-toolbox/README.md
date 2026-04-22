# Basic example of hosting an agent with the `responses` API and Foundry Toolbox

## Creating a Foundry Toolbox

You can create a Foundry Toolbox by code. Refer to this sample for an example: [Foundry Toolbox CRUD Sample](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/ai/azure-ai-projects/samples/hosted_agents/sample_toolboxes_crud.py).

You can also create a Foundry Toolbox in the Foundry portal. Read more [here](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox).

## Deploying to Foundry

Create a new directory and initialize a Foundry Agent project in it:

```bash
mkdir my-foundry-agent
cd my-foundry-agent
azd ai agent init -m https://github.com/microsoft/hosted-agents-vnext-private-preview/blob/main/samples/python/hosted-agents/agent-framework/responses/04-foundry-toolbox/agent.manifest.yaml
```

Follow the prompts to complete the initialization. Then create the necessary resources by running:

```bash
azd provision
```

The above will create the toolbox with the specified tools in `agent.manifest.yaml`.

Then deploy the agent by running:

```bash
azd deploy
```

## Running the server locally

### Using `azd` (Recommended)

```bash
azd ai agent run
```

### Without `azd`

Follow the instructions in the [Environment setup](../../README.md#environment-setup-without-azd) section of the README in the parent directory to set up your environment and install dependencies.

Run the following command to start the server:

```bash
python main.py
```

## Interacting with the agent

Send a POST request to the server with a JSON body containing a "message" field to interact with the agent. For example:

```bash
azd ai agent invoke --local "What tools do you have?"
```

Or use `curl`:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "What tools do you have?"}'
```
