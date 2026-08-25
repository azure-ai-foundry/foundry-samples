# Hello World Autopilot

A minimal Microsoft Foundry hosted agent that responds to Microsoft Teams
messages through the Activity protocol. It recognizes:

- Teams direct messages
- Teams group chat messages
- Teams channel messages that tag the agent

## How it works

`agent/app.py` hosts the M365 Agents SDK application and sends each supported
message to a model deployment in an existing Foundry project. `azure.yaml` lets
`azd` deploy the Python agent into the project identified by the active
environment.

## Run this sample

Follow the shared [setup, deployment, publication, and approval
instructions](../README.md).

### Configure an existing Foundry project

Create an environment in this directory and configure the existing project and
model deployment:

```powershell
azd env new my-hello-world
azd env set AZURE_SUBSCRIPTION_ID <subscription-id>
azd env set AZURE_LOCATION <foundry-project-region>
azd env set AZURE_AI_PROJECT_ID <foundry-project-resource-id>
azd env set FOUNDRY_PROJECT_ENDPOINT <foundry-project-endpoint>
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME <model-deployment-name>
```

Copy the project resource ID, endpoint, and region from **Manage** > **Project
details** in the Foundry portal. `AZURE_SUBSCRIPTION_ID` is required separately
even though the project resource ID contains it, and `AZURE_LOCATION` must match
the existing project's region.

This sample does not own the project or model deployment. Do not run
`azd provision` or `azd up`.

### Sample-specific commands

The deployed agent name is `hello-world-autopilot`. Use it when stopping
sessions after a code deployment:

```powershell
..\scripts\stop-agent-sessions.ps1 -AgentName hello-world-autopilot
```

Use this sample's metadata when publishing:

```powershell
python ..\scripts\publish_autopilot.py `
  --display-name "Hello World Autopilot" `
  --short-description "A minimal Microsoft 365 Autopilot agent." `
  --full-description "A Microsoft Foundry agent that responds to Teams messages."
```

## Observability

The agent initializes the
[Microsoft OpenTelemetry Distro](https://learn.microsoft.com/microsoft-agent-365/developer/microsoft-opentelemetry?tabs=python)
before importing the application stack.

- **Foundry traces:** Foundry injects `APPLICATIONINSIGHTS_CONNECTION_STRING`
  into the hosted container. The distro detects it and exports application,
  Microsoft Agents SDK, HTTP, Azure SDK, and model-call telemetry to Azure
  Monitor. This is the telemetry used by the Foundry traces experience.
- **Agent 365:** Activity baggage middleware adds agent, tenant, user, channel,
  session, and conversation context. Output middleware records response spans,
  and the Agent 365 exporter sends the enriched telemetry used by Microsoft 365
  administration, Defender, and Purview experiences.

## Configuration

The current `azure.ai.agent` manifest schema requires a literal agent name, so
this sample deploys as `hello-world-autopilot`.

## Key files

| File | Purpose |
| --- | --- |
| `azure.yaml` | Hosted-agent deployment configuration |
| `main.py` | Direct code deployment entry point |
| `agent/app.py` | Activity handlers, authentication, and model invocation |
| `agent/activity_routing.py` | Selectors for supported Teams conversations |
| `agent/observability.py` | Microsoft OpenTelemetry configuration |
| `requirements.txt` | Fully resolved Python runtime dependencies |

## Troubleshooting

- If Teams works but the Foundry traces page has no application spans, confirm
  the hosted container received `APPLICATIONINSIGHTS_CONNECTION_STRING` and
  inspect the session logs for Microsoft OpenTelemetry exporter errors.
- If Azure Monitor traces appear but Agent 365 export returns 401 or 403,
  verify both tenant admin consent and managed-blueprint inheritance for
  `Agent365.Observability.OtelWrite`.
