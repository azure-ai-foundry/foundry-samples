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

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../../README.md) for more details. Use this README for sample queries you can send to the agent.

Send a POST request to the server with a JSON body containing an "input" field to interact with the agent. For example:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Create a colorful 3-day PDF travel guide for Lisbon focused on food, viewpoints, and neighborhoods."}'
```

The script writes the PDF to `$HOME/generated-travel-guides` by default and returns a `$HOME`-based file path such as `$HOME/generated-travel-guides/lisbon-3-day-travel-guide.pdf`. `$HOME` is the Foundry hosted-agent convention for locating generated outputs. For production scenarios that need durable external sharing, update the skill script to upload the PDF to storage and return a shareable URL.

## Deploying the Agent to Foundry

To host the agent on Foundry, follow the instructions in the [Deploying the Agent to Foundry](../../README.md#deploying-the-agent-to-foundry) section of the README in the parent directory.
