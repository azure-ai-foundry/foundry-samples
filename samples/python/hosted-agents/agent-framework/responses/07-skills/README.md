# What this sample demonstrates

An [Agent Framework](https://github.com/microsoft/agent-framework) agent with **native file-based skills** hosted using the **Responses protocol**. It shows how to add a `SkillsProvider` to an agent so skills in a local `skills/` folder are discovered automatically and can use the same Azure credentials as the rest of the Foundry agent.

The included `travel-guide` skill can create a colorful PDF city travel guide by running a bundled Python script. The script uses only the Python standard library so there is no extra PDF package to install.

## How It Works

### Model Integration

The agent uses `FoundryChatClient` from the Agent Framework to create a Responses client from the project endpoint and model deployment. The agent supports both streaming (SSE events) and non-streaming (JSON) response modes.

See [main.py](main.py) for the full implementation.

### Skills

The sample creates a `SkillsProvider` pointed at the local [skills](skills/) directory:

```python
skills_provider = SkillsProvider(
    skill_paths=Path(__file__).parent / "skills",
    script_runner=run_local_skill_script,
)
```

Agent Framework discovers the [travel-guide](skills/travel-guide/) skill from its `SKILL.md` file and advertises it to the model. When the user asks for a travel guide, the model can load the skill instructions and run `scripts/create_travel_guide.py` through the configured script runner.

### Agent Hosting

The agent is hosted using the [Agent Framework](https://github.com/microsoft/agent-framework) with the `ResponsesHostServer`, which provisions a REST API endpoint compatible with the OpenAI Responses protocol.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../../README.md#running-the-agent-host-locally) section of the README in the parent directory to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell), `azd`, or the **Agent Inspector** in the Foundry Toolkit VS Code extension. Please refer to the [parent README](../../README.md) for more details. Use this README for sample queries you can send to the agent.

Send a POST request to the server with a JSON body containing an "input" field to interact with the agent. For example:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Create a colorful 3-day PDF travel guide for Lisbon focused on food, viewpoints, and neighborhoods."}'
```

The script writes the PDF to `$HOME/generated-travel-guides` by default and returns a `$HOME`-based file path such as `$HOME/generated-travel-guides/lisbon-3-day-travel-guide.pdf`. `$HOME` is the Foundry hosted-agent convention for locating generated outputs. For production scenarios that need durable external sharing, update the skill script to upload the PDF to storage and return a shareable URL.

### Test in Agent Inspector

Once the agent is running locally, open **Agent Inspector** in VS Code (Command Palette: **Foundry Toolkit: Open Agent Inspector**) to interactively send messages and view responses.

Type the following message in Inspector:

```
Create a colorful 3-day PDF travel guide for Lisbon focused on food, viewpoints, and neighborhoods.
```

## Deploying the Agent to Foundry

To host the agent on Foundry, follow the instructions in the [Deploying the Agent to Foundry](../../README.md#deploying-the-agent-to-foundry) section of the README in the parent directory.

### Deploying with the Foundry Toolkit VS Code Extension

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Deploy Hosted Agent**. The extension opens a tab-based **Deploy Hosted Agent** wizard and reads `agent.yaml` to auto-populate what it can.
2. If prompted, complete **Foundry Project Setup** to pick the subscription and Foundry project (or create a new one) to deploy to.
3. On the **Basics** tab, configure the core deployment settings:
   - **Deployment Method**: **Code** (upload as a ZIP) or **Container** (Docker image via ACR).
   - For **Code**, pick a packaging option: **Remote** or **Local**.
   - For **Container**, pick a registry option: default ACR, your own ACR, or a prebuilt ACR image.
   - **Hosted Agent Name**: confirm the name to register with the hosting service.
4. On the **Review + Deploy** tab, finalize the runtime and resources:
   - Confirm the auto-detected runtime details (language, entry point, or Dockerfile).
   - Pick a **CPU and Memory** size.
   - Click **Deploy**. Fields are validated inline, and the extension handles the build/upload, agent version creation, and RBAC role assignment.
5. After deployment, invoke the agent in the Agent Playground and stream live logs from the **Logs** tab.
