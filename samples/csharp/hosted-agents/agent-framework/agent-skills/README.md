# What this sample demonstrates

An [Agent Framework](https://github.com/microsoft/agent-framework) agent that loads its behavioral guidelines from [**Foundry Skills**](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/skills) at startup, hosted using the **Responses protocol**. Skills are authored once as `SKILL.md` files, uploaded to your Foundry project through the Skills REST API, and downloaded by the agent on boot so updates ship without code changes.

## How It Works

### Authoring skills

Each skill is a Markdown file with a YAML front matter block. This sample ships two source skills under [`skills/`](skills/):

| Skill | Purpose |
|---|---|
| [`support-style`](skills/support-style/SKILL.md) | Voice, formatting, and signature rules for Contoso Outdoors support replies. |
| [`escalation-policy`](skills/escalation-policy/SKILL.md) | When and how to escalate a customer ticket. |

Each `SKILL.md` includes a unique `*-CANARY-*` token that the model is asked to echo, so you can prove the skill was loaded from Foundry (not hallucinated) by checking the response.

> The `name` and `description` values in the YAML front matter must be **unquoted** — quoting them causes the Skills REST API to return HTTP 500 on import.

### Uploading skills (sample convenience only)

The sample includes a convenience provisioning step that checks whether each skill exists in Foundry and uploads it if not, gated behind the `PROVISION_SAMPLE_SKILLS=true` env var. **In production, skill provisioning is an external concern** — it is NOT the hosted agent's responsibility. A real deployment pipeline would provision skills separately (for example via a CI/CD step, a CLI script, or a management portal).

The provisioning uses `ProjectAgentSkills.CreateSkillFromPackageAsync(directoryPath)` from the `Azure.AI.Projects.Agents` SDK. The method packages the `SKILL.md` directory as a ZIP and uploads it to Foundry.

> **Preview opt-in.** The Foundry Skills API is currently a preview surface and requires the `Foundry-Features: Skills=V1Preview` opt-in header on every request (uploads, lookups, and downloads). The `Azure.AI.Projects` SDK does not yet inject this on the Skills sub-client, so the sample registers a small `FoundryFeaturesPolicy` pipeline policy on the `AgentAdministrationClient`. Once the SDK starts emitting the header by default, the policy can be removed.

### Downloading skills at agent startup

[`Program.cs`](src/agent-skills/Program.cs) reads the comma-separated `SKILL_NAMES` env var and, for each skill name, downloads the ZIP archive from Foundry via `ProjectAgentSkills.DownloadSkillAsync(name)`, then unpacks it into a **separate runtime directory** at `downloaded_skills/<name>/` (kept distinct from the static `skills/` source folder).

An `AgentSkillsProvider` is then built over `downloaded_skills/` and attached to the agent as an `AIContextProvider`. The provider follows the [Agent Skills](https://agentskills.io/) progressive-disclosure pattern:

1. **Advertise** — skill names and descriptions are injected into the system prompt at session start (around 100 tokens per skill).
2. **Load** — the model calls the `load_skill` tool when it decides a skill is relevant to the user's turn, and the full `SKILL.md` body is returned.

The model only pays the token cost for a skill's full body when it actually needs it, and updating a skill in Foundry plus restarting the agent is enough to pick up the change — no code redeploy required.

> **Note:** This sample supports instruction-only skills. If your downloaded skills contain resource files or scripts, configure the corresponding readers when constructing the `AgentSkillsProvider`.

### Agent Hosting

The agent is hosted using the [Agent Framework](https://github.com/microsoft/agent-framework) with the Responses API hosting layer (`AddFoundryResponses` / `MapFoundryResponses`).

See [Program.cs](src/agent-skills/Program.cs) for the full implementation.

## Running the Agent Locally

### Prerequisites

Before running this sample, ensure you have:

1. **Azure Developer CLI (`azd`)**
   - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) (1.25 or later) and the unified Foundry CLI extension: `azd ext install microsoft.foundry`
   - Authenticated: `azd auth login`

2. **Azure CLI**
   - Installed and authenticated: `az login`

3. **.NET 10.0 SDK or later**
   - Verify your version: `dotnet --version`
   - Download from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

> [!NOTE]
> You do **not** need an existing [Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/what-is-foundry?view=foundry) project or model deployment to get started — `azd provision` creates them for you. If you already have a project, see the [note below](#using-azd) on how to target it.

### Required RBAC

Your identity (or the Managed Identity running the container in production) needs **Azure AI User** on the Foundry project scope. This single role covers both authoring skills and downloading them.

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint. Auto-injected in hosted containers; set automatically by `azd ai agent run` locally. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | Model deployment name — must match a deployment in your Foundry project. Declared in `azure.yaml`. |
| `SKILL_NAMES` | Yes | Comma-separated list of Foundry skill names to download at startup (for example `support-style,escalation-policy`). |
| `PROVISION_SAMPLE_SKILLS` | No | Sample convenience: set to `true` on a first run to upload this sample's `SKILL.md` files to Foundry. Leave unset or false in production. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recommended | Enables telemetry. Auto-injected in hosted containers; set manually for local dev. |

**Local development (without `azd`):**

```bash
# Set env vars directly — .NET does not natively read .env files
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="<your-model-deployment-name>"
export SKILL_NAMES="support-style,escalation-policy"
export PROVISION_SAMPLE_SKILLS=true   # First run only
```

> [!NOTE]
> When using `azd ai agent run`, environment variables are handled automatically — no manual setup needed.

### Installing Dependencies

> [!NOTE]
> If using `azd ai agent run`, dependencies are restored automatically — skip to [Running the Sample](#running-the-sample).

```bash
dotnet restore
```

### Running the Sample

Run and test hosted agents locally with the Azure Developer CLI (`azd`) or the Foundry VS Code extension.

<details>
<summary><h4>Using the Foundry VS Code Extension</h4></summary>

The [Foundry VS Code extension](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) has a built-in sample gallery. You can open this sample directly from the extension without cloning the repository — it scaffolds the project into a new workspace, generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically, and configures a one-click **F5** debug experience.

Follow the [VS Code quickstart](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) for a full step-by-step walkthrough.

</details>

#### Using [`azd`](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd)

No cloning required. Create a new folder, point `azd` at the manifest on GitHub, and it sets up the sample and adopts its `azure.yaml` as the project manifest and configures your environment automatically:

```bash
# Create a new folder for the agent and navigate into it
mkdir agent-skills && cd agent-skills

# Initialize from the manifest - azd reads it, downloads the sample,
# and adopts its azure.yaml as the project manifest and configures your environment.
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/csharp/hosted-agents/agent-framework/agent-skills/azure.yaml

# Provision Azure resources (Foundry project, model deployment, App Insights).
azd provision

# Tell azd which skills to download at startup.
azd env set SKILL_NAMES "support-style,escalation-policy"

# First run only - upload the sample's local SKILL.md files to Foundry.
azd env set PROVISION_SAMPLE_SKILLS true

# Run the agent locally (handles env vars, build, and startup).
azd ai agent run
```

> [!NOTE]
> If you've already cloned this repository, pass a local path to the manifest instead:
> `azd ai agent init -m <path-to-repo>/samples/csharp/hosted-agents/agent-framework/agent-skills/azure.yaml`

On startup you should see:

```text
Skill 'support-style' already exists in Foundry.
Skill 'escalation-policy' already exists in Foundry.
Downloading skill 'support-style' from Foundry...
Downloading skill 'escalation-policy' from Foundry...
```

The agent starts on `http://localhost:8088/`. To invoke it:

```bash
azd ai agent invoke --local "Hi, I am Alex. Can I return my tent within 30 days?"
```

Or use curl directly:

```bash
curl -sS -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "Hi, I am Alex. Can I return my tent within 30 days?", "stream": false}' | jq .
```

| Prompt mentions | Skill that should drive the response |
|---|---|
| Routine return / shipping / care question | Model loads `support-style` (canary `STYLE-CANARY-3318`) — no escalation. |
| Injury, legal threat, press, or refund > $500 | Model loads `escalation-policy` (canary `ESC-CANARY-7742`) **and** `support-style`. |

Because skills are loaded on demand, the canary token in a response also proves the model actually invoked `load_skill` for the matching skill — not just saw its name in the advertised list.

#### Manual setup

If running without `azd`, set environment variables manually (see [Environment Variables](#environment-variables)), then:

```bash
dotnet run
```

### Deploying the Agent to Microsoft Foundry

Once you have tested locally, deploy to Microsoft Foundry:

```bash
# Provision Azure resources (skip if already done during local setup).
azd provision

# Make sure SKILL_NAMES is set in your azd environment so it is injected into the hosted container.
azd env set SKILL_NAMES "support-style,escalation-policy"

# Build, push, and deploy the agent to Foundry.
azd deploy
```

After deploying, invoke the agent running in Foundry:

```bash
azd ai agent invoke "Hi, I am Alex. Can I return my tent within 30 days?"
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

> The `skills/` source folder is **not** consumed by the deployed agent at runtime — only the skills already present in Foundry are downloaded. The provisioning step (or your own pipeline) must have uploaded the named skills to the same Foundry project before the agent starts.

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

**Deploy with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
