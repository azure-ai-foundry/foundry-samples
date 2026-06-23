<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. 
<!-- End standard disclaimer -->

# What this sample demonstrates

A hosted hotel-booking agent built with the **Invocations protocol** in Python, designed for **Voice Live compatible**.

The sample shows how to:

- Accept speech input via `input_audio.transcription` events.
- Accept UI/button actions via JSON click events (`select_hotel`, `confirm_booking`, `cancel`).
- Maintain per-session state for multi-turn conversations.
- Stream responses as SSE with `output_audio_transcription.delta` / `.done` plus custom UI events.

This sample uses a small in-memory hotel catalog and keyword-based intent handling. It does not require a model call.

## How it works

The agent in [main.py](main.py) uses the [Azure AI AgentServer Invocations SDK](https://pypi.org/project/azure-ai-agentserver-invocations/) to host an invocations endpoint.

At runtime it handles two input paths:

1. Voice path: `{"type":"input_audio.transcription","input":"..."}`
2. Click/UI path: arbitrary JSON with an `action` property

Depending on session state, the agent emits:

- Spoken text streaming events (`output_audio_transcription.delta`, then `.done`)
- Custom typed UI events (`ui.hotel_cards`, `ui.hotel_detail`, `ui.action_buttons`, `ui.booking_confirmed`, `ui.booking_update`)
- Final `done`

## Running the agent locally

### Prerequisites

Before running this sample, ensure you have:

1. **Azure Developer CLI (`azd`)**
	 - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) and the AI agent extension: `azd ext install azure.ai.agents`
	 - Authenticated: `azd auth login`
2. **Python 3.10 or later**
	 - Verify your version: `python --version`

### Environment variables

This sample can run without extra environment variables.

See [`.env.example`](.env.example). `FOUNDRY_PROJECT_ENDPOINT` is optional and only needed in scenarios where your workflow expects it.

### Install dependencies

```bash
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

### Start the agent

```bash
python main.py
```

The service listens on `http://localhost:8088`.

### Test with curl

Send a voice transcription event:

```bash
curl -sS -N -X POST "http://localhost:8088/invocations" \
	-H "Content-Type: application/json" \
	-d '{"type":"input_audio.transcription","input":"Find me a hotel in Seattle"}'
```

Use the same `agent_session_id` across turns to keep conversation and booking state.

## Using `azd`

No cloning required. Create a new folder, initialize from the manifest, then provision and run:

```bash
mkdir hotel-booking-agent && cd hotel-booking-agent

azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/bring-your-own/voicelive/hotel-booking-invocations-voicelive/agent.manifest.yaml

azd provision
azd ai agent run
```

Then invoke locally:

```bash
azd ai agent invoke --local '{"type":"input_audio.transcription","input":"Find me a hotel in Seattle"}'
```

## Deploying to Microsoft Foundry

```bash
azd provision
azd deploy
```

After deployment:

```bash
azd ai agent invoke '{"type":"input_audio.transcription","input":"Find me a hotel in Seattle"}'
```

Stream logs:

```bash
azd ai agent monitor
```

For full deployment guidance, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

## Notes for production use

- Session state is in-memory and resets when the process restarts.
- Replace the in-memory store with Redis/Cosmos DB for durable sessions.
- Replace keyword intent matching with model-based NLU for robust behavior.
