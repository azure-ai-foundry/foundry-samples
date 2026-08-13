# Microsoft Foundry Agent — Golden Path (end to end)

This guide sequences the existing infrastructure-setup samples and SDK quickstarts
into a single **end-to-end golden path** for standing up a Foundry Agent, for both
**non-VNet (public)** and **VNet (network-secured)** deployments.

The six steps are:

1. Create a Foundry resource (Cognitive Services / Foundry account)
2. Create a project
3. Create a VNet *(VNet path only)*
4. Create APIM and add it to the project (AI Gateway)
5. Bring your own model (BYOM) through the gateway
6. Create an agent

Each step below points at the exact template or sample to run. Steps 1–5 are Bicep
(`az deployment group create`); step 6 is SDK code (Python / C# / Java).

---

## Path A — Non-VNet (public networking)

Steps **1–2** (account + project) and **step 6** (create an agent) are identical across
every BYOM variant — only the **BYOM connection** in steps 4–5 changes. Pick the variant
that matches where your model lives and whether you front it with a gateway.

### Steps 1–2 — Foundry account + project

Deploy [`41-standard-agent-setup`](../41-standard-agent-setup/) (standard, BYO deps) or
[`40-basic-agent-setup`](../40-basic-agent-setup/) (basic, platform-managed). Note the
**account name**, **project resource ID**, and the project's **managed-identity client ID**
(`az resource show` on the project, or Foundry portal → project → Identity).

*(Step 3 — create a VNet — is N/A for the public path.)*

### Steps 4–5 — Bring your own model (pick one variant)

| # | BYOM variant | When to use | Sample to run | Auth |
|---|--------------|-------------|---------------|------|
| 1 | **APIM gateway** + Foundry model in another project | You want an AI Gateway (APIM) in front of a Foundry / Azure OpenAI model hosted in another account/project | [`01-connections/public-byom-apim`](../01-connections/public-byom-apim/) — creates APIM + role assignment + BYOM connection | Project MI |
| 2 | **No separate gateway** — model in another Foundry / Azure OpenAI account | Reach a model that lives in another account/project without standing up your own APIM — point a **ModelGateway** connection straight at that account's inference endpoint | [`01-connections/model-gateway`](../01-connections/model-gateway/) (`samples/parameters-foundryazureai.json`, `parameters-foundryopenai.json`, or `parameters-foundryanthropic.json`) | API key / OAuth2 |
| 3 | **Third-party model provider** | Model hosted by a 3P provider (e.g. OpenAI) reached through the Foundry **ModelGateway** connection | [`01-connections/model-gateway`](../01-connections/model-gateway/) (`samples/parameters-openai.json`) | API key / OAuth2 |
| 4 | **BYOM + BYOG** (your own 3P gateway) | Your own non-APIM gateway (LiteLLM, Kong, a custom proxy, …) in front of your models | [`01-connections/model-gateway`](../01-connections/model-gateway/) (custom `targetUrl` + headers / auth) | API key / OAuth2 |

All four produce a **connection** whose model is referenced as `<connectionName>/<modelName>`
by the agent in step 6 (e.g. `ai-gateway/gpt-4o`).

> [!IMPORTANT]
> **BYOM `<connection>/<model>` resolution only works for *gateway-category* connections —
> `ApiManagement` (variant 1) or `ModelGateway` (variants 2–4).** A plain `AzureOpenAI` or
> `CognitiveService`/Foundry connection (`connection-azure-openai.bicep` /
> `connection-foundry.bicep`) is **not** resolvable by a prompt agent as `<connection>/<model>`
> — a prompt agent bound to one fails with `Connection '<name>' not found`. Those connection
> types are for classic SDK / data-plane use, not gateway-routed agent inference. To reach a
> model in *another* account without your own APIM, use variant 2 (a `ModelGateway` connection
> pointed directly at that account's inference endpoint).

> [!NOTE]
> Variant 1 is the only one that *creates* the gateway infra for you (public APIM + role
> assignment). Variants 2–4 add a **ModelGateway** connection to an upstream endpoint that
> already exists (another Foundry/Azure OpenAI account, a 3P provider, or your own gateway).
> In every case the model is invoked through a **prompt agent + Responses API** — see
> [Step 6](#step-6--create-an-agent-both-paths).

### Step 6 — create an agent

Identical for every variant — see [Step 6](#step-6--create-an-agent-both-paths). Reference the
model as `<connectionName>/<modelName>` and run the connection-agnostic
[`public-byom-apim/samples/create-agent.py`](../01-connections/public-byom-apim/samples/create-agent.py)
(pass `--endpoint` for your project and `--model <connectionName>/<modelName>` for whichever
variant you deployed).

---

## Path B — VNet (network-secured)

| Step | What | Sample to run |
|------|------|---------------|
| 1 + 2 + 3 | Foundry account + project + VNet (+ PEs, DNS, agent infra) | [`15-private-network-standard-agent-setup`](../15-private-network-standard-agent-setup/) (standard) or [`11-private-network-basic-vnet`](../11-private-network-basic-vnet/) (basic) |
| 4 + 5 | Add an existing APIM to the private endpoint and DNS foundation | [`16-private-network-standard-agent-apim-setup`](../16-private-network-standard-agent-apim-setup/) (does not create the APIM API, policies, or BYOM connection) |
| 4 + 5 | Create the APIM AI Gateway and connect a model in another region | [`16-.../extensions/byom-cross-region`](../16-private-network-standard-agent-apim-setup/extensions/byom-cross-region/) (private model path) |
| 4 + 5 | Connect directly to a model in another Foundry account | The same [`byom-cross-region`](../16-private-network-standard-agent-apim-setup/extensions/byom-cross-region/) extension with `enableDirectFoundryConnection = true` |
| 4 + 5 | Connect to a third-party OpenAI-compatible provider | The same [`byom-cross-region`](../16-private-network-standard-agent-apim-setup/extensions/byom-cross-region/) extension with `enableThirdPartyConnection = true` |
| 6 | Create an agent | Use the [prompt-agent + Responses API flow](#step-6--create-an-agent-both-paths) |

### Notes for the VNet path

- **Steps 1–3** are delivered together by templates `11`/`15`/`16` (account, project,
  BYO VNet, private endpoints, DNS, RBAC, capability host).
- **Steps 4 + 5, APIM path** — the private, VNet-integrated APIM + BYOM connection are delivered by
  the [`byom-cross-region`](../16-private-network-standard-agent-apim-setup/extensions/byom-cross-region/)
  extension. It stands up a StandardV2 APIM (outbound VNet-integrated), a backend Foundry
  account, a cross-region private endpoint, the `/inference` API policy chain, and the BYOM
  connection — end to end private (`publicNetworkAccess = Disabled` on the backend).
  - If you already have a private APIM you only need to **connect**, use
    [`01-connections/apim`](../01-connections/apim/) instead.
- **Steps 4 + 5, direct and third-party paths** — the extension reuses the shared
  [`01-connections/model-gateway`](../01-connections/model-gateway/) module. These connected-model
  calls originate from the managed Agent Service inference plane, not the delegated agent subnet,
  so their upstream endpoints must be publicly reachable. The project dependencies remain
  network-secured by the VNet and project capability host, but these two model paths are not
  end-to-end private.
- **Step 6** is identical to the public path — the [prompt-agent + Responses API flow](#step-6--create-an-agent-both-paths)
  is networking-agnostic; the private connectivity is already established by the infra.

---

## Step 6 — Create an agent (both paths)

> [!IMPORTANT]
> **A BYOM (gateway-connected) model only works with a *prompt agent* invoked through the
> Responses API.** The classic Assistants API (`create_agent` + threads + runs) **cannot**
> resolve a `<connection>/<model>` reference and fails with
> `invalid_engine_error: Failed to resolve model info`. Use the flow below — not the generic
> `create-agent` (assistants) quickstart — when the model comes from the AI Gateway.

Point the SDK at your **project endpoint** and reference the model as
`<connection-name>/<model-name>` (e.g. `ai-gateway/gpt-4o`) from step 5. The agent runs
server-side using the **project's managed identity**, which mints the token that APIM's
`validate-azure-ad-token` policy checks.

```python
# pip install azure-ai-projects>=2.0.0 azure-identity
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition

ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"
MODEL = "ai-gateway/gpt-4o"          # <connection-name>/<model-name> from step 5

project = AIProjectClient(endpoint=ENDPOINT, credential=DefaultAzureCredential())

# 1. Create a PROMPT agent version bound to the gateway model
agent = project.agents.create_version(
    agent_name="gateway-agent",
    definition=PromptAgentDefinition(
        model=MODEL,
        instructions="You are a helpful assistant.",
    ),
)

# 2. Invoke it through the Responses API (NOT threads/runs)
client = project.get_openai_client()
conversation = client.conversations.create()
response = client.responses.create(
    conversation=conversation.id,
    input="Say hi in five words.",
    extra_body={"agent_reference": {"name": agent.name, "type": "agent_reference"}},
)
print(response.output_text)
```

A runnable version of this script ships with the public BYOM sample at
[`01-connections/public-byom-apim/samples/create-agent.py`](../01-connections/public-byom-apim/samples/create-agent.py).

**Prerequisites for step 6 to succeed** (all created by steps 4–5, but verify if you see errors):

- The BYOM connection has **`audience`** set (e.g. `https://cognitiveservices.azure.com`).
  Missing it fails with `Project identity requires an audience to be specified`.
- The APIM managed identity has **`Cognitive Services User`** on the backend account.
- The APIM `validate-azure-ad-token` policy uses the **project MI application (client) ID**.
- The backend model deployment referenced in the connection (e.g. `gpt-4o`) exists.

---

## Which path should I use?

| | Non-VNet (Path A) | VNet (Path B) |
|--|-------------------|---------------|
| Public network access | Enabled | Disabled (private endpoints) |
| Setup complexity | Lower | Higher |
| Model traffic on the Microsoft backbone | No | Yes (private endpoints) |
| Best for | Dev / test, public workloads, quickest AI Gateway onboarding | Regulated / network-secured production workloads |

Both paths converge on the same agent-creation code — only the infrastructure underneath differs.
