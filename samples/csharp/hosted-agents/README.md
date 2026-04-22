# Microsoft Foundry — Hosted Agent Samples (.NET)

Samples for building, deploying, and managing hosted agents on [Microsoft Foundry](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents). Each sample is a starter template — fork it, change the system prompt and tools, deploy with `azd up`.

> **Every sample includes Application Insights and OpenTelemetry tracing out of the box.** You get production-ready logging, distributed traces, and metrics from the first sample you run.

### Quickstart

> **Prerequisites:** Install the Azure Developer CLI with the Foundry AI extension. See [Set up azd for hosted agents](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd) if you haven't already.

```bash
mkdir my-agent && cd my-agent
azd ai agent init -m ../agent-framework/hello-world/agent.manifest.yaml
azd up
```

You'll have a running agent in minutes. Or, if you prefer VS Code, use the [Foundry extension quickstart](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=vscode) to build and deploy directly from the editor.

Read on to pick the right sample for your scenario, or jump to the [learning path](#learning-path) for a guided walkthrough.

---

## Two protocols: Responses and Invocations

Hosted agents support two protocols. Pick the one that matches your scenario.

| Scenario | Protocol | Why |
|----------|----------|-----|
| Conversational chatbot or assistant | **Responses** | The platform manages conversation history, streaming events, and session lifecycle — use any OpenAI-compatible SDK as the client. |
| Agent published to Teams or M365 | **Responses** + **Activity** | The Responses protocol powers the agent logic; the Activity protocol handles the Teams channel integration. |
| Multi-turn Q&A with RAG or tools | **Responses** | Built-in `conversation_id` threading and tool result handling. |
| Background / async processing | **Responses** | `background: true` with platform-managed polling and cancellation — no custom code needed. |
| Webhook receiver (GitHub, Stripe, Jira, etc.) | **Invocations** | The external system sends its own payload format — you can't change it to match `/responses`. |
| Non-conversational processing (classification, extraction, batch) | **Invocations** | The input is structured data, not a chat message. Arbitrary JSON in, arbitrary JSON out. |
| Custom streaming protocol (AG-UI, etc.) | **Invocations** | AG-UI and other agent-UI protocols aren't OpenAI-compatible — you need raw SSE control. |
| Async job with custom progress, polling, or non-OpenAI callers | **Invocations** | Custom progress reporting, intermediate results, and polling semantics beyond what Responses `background: true` provides. |
| Protocol bridge (GitHub Copilot, proprietary systems) | **Invocations** | The caller has its own protocol that doesn't map to `/responses`. |
| Inter-service orchestration (Durable Functions, Logic Apps) | **Invocations** | The caller sends structured task payloads, not chat messages. |

> **Still not sure?** Start with **Responses**. You can always add an Invocations endpoint later — a hosted agent can support both protocols simultaneously by listing both in `agent.yaml`.

> **Other protocols:** Hosted agents can also expose the **Activity** protocol (for Teams and M365 integration) and the **A2A** protocol (for agent-to-agent delegation).

<details>
<summary><strong>Protocol comparison details</strong></summary>

| | **Responses** | **Invocations** |
|---|---|---|
| **Best for** | Most agents — the platform manages conversation history, streaming lifecycle, and background polling | Agents that need full HTTP control, custom payloads, or custom async workflows |
| **Payload** | OpenAI-compatible `/responses` contract | Arbitrary JSON via `/invocations` — you define the schema |
| **Client SDK** | Any OpenAI-compatible SDK (Python, JS, C#) works out of the box | Custom client — you define the contract |
| **Session history** | Framework-managed via `conversation_id` | You manage sessions (in-memory, Cosmos DB, etc.) |
| **Streaming** | Framework-managed `ResponseEventStream` with lifecycle events (`created`, `in_progress`, `delta`, `completed`) | Raw SSE — you format and write events directly |
| **Background / long-running** | Built-in (`background: true` + platform-managed polling) | Manual task tracking and custom polling endpoints |
| **Server SDK** | `azure-ai-agentserver-responses` | `azure-ai-agentserver-invocations` |
| **agent.yaml** | `protocol: responses`, `version: v0.1.0` | `protocol: invocations`, `version: v0.0.1` |

</details>

---

## Pick your framework

Hosted agents run any code you can put in a container. These samples cover three frameworks — pick the one that matches where you are.

| | **Agent Framework** | **Bring Your Own** |
|---|---|---|
| **Best for** | Starting fresh on Foundry — also supports AutoGen and Semantic Kernel | Already built with your own .NET stack or framework |
| **SDK** | `Microsoft.Agents.AI.Foundry` + `Azure.AI.Projects` | `azure-ai-agentserver-responses` / `azure-ai-agentserver-invocations`, or `azure-ai-agentserver-core` for fully custom HTTP |
| **Foundry integration** | Native — sessions, tools, memory, streaming all built in | Core adapter hosts the web server and exposes `/invocations` and `/responses` endpoints; you supply the agent logic |
| **Protocols** | Responses and Invocations | Responses and Invocations |
| **Language support** | C# and Python | Any language (C# and Python samples provided) |
| **Start here** | [Hello World →](agent-framework/hello-world/) | [Hello World →](bring-your-own/responses/HelloWorld/) |

> **Which should I choose?** If you're building a new agent — or already using AutoGen or Semantic Kernel — start with **Agent Framework**. It has the tightest Foundry integration, supports those orchestrators natively, and has the most samples to learn from. If you have existing .NET agent code, **Bring Your Own** shows how to containerize and deploy it unchanged. For LangGraph or CrewAI (Python-only frameworks), see the [Python samples](../../python/hosted-agents/).

---

## Agent Framework samples

The recommended path for building hosted agents. Agent Framework gives you native session management, built-in tool wiring, streaming, and the full Foundry feature set.

Samples are split by protocol. Start with **Responses** (the common path) — then explore **Invocations** when you need full HTTP control or long-running workflows.

### Responses protocol

The platform manages conversation history, streaming lifecycle, and background execution. This is the default for most agents.

#### Which sample should I start with?

| I want to... | Start here | Then try |
|--------------|-----------|----------|
| Get a hosted agent running as fast as possible | [Hello World](agent-framework/hello-world/) | Multi-Turn Sessions → Tools |
| Build a chatbot that remembers conversations | [Multi-Turn Sessions](agent-framework/simple-agent/) | Tools, Knowledge Grounding |
| Connect an agent to APIs, MCP servers, or search | [Tools](agent-framework/local-tools/) | MCP Tools, Knowledge Grounding |
| Use client-side or server-side MCP patterns | [MCP Tools](agent-framework/mcp-tools/) | Tools, Knowledge Grounding |
| Answer questions from my own documents | [Knowledge Grounding](agent-framework/text-search-rag/) | Tools |
| Build a multi-agent workflow with routing | [Workflows](agent-framework/workflows/) | Agent-to-Agent |

#### Learning path

**New to hosted agents?** Start here and work through in order:

1. **[Hello World](agent-framework/hello-world/)** — Deploy your first agent, invoke it, see traces in App Insights.
2. **[Multi-Turn Sessions](agent-framework/simple-agent/)** — Adds multi-turn conversation history.
3. **[Tools](agent-framework/local-tools/)** — Add local C# function tools to your agent.

**Ready for more?**

4. **[Knowledge Grounding](agent-framework/text-search-rag/)** — Ground answers in your own documents with TextSearchProvider.
5. **[MCP Tools](agent-framework/mcp-tools/)** — Connect to MCP servers using client-side and server-side MCP patterns.
6. **[Workflows](agent-framework/workflows/)** — Compose multiple agents into sequential pipelines.

### Invocations protocol

Full control over the HTTP request/response cycle. You define the payload schema, manage session state, and implement polling for long-running operations. Use this when you need an arbitrary payload format or async workflows that don't fit the OpenAI `/responses` contract.

> **Every capability works with both protocols.** Tools, RAG, memory, evaluations, Teams publishing, multi-agent — all of these work with Invocations. The Invocations samples below focus on the protocol mechanics (how you handle requests, streaming, sessions, and long-running tasks). To add a capability like knowledge grounding or tools, learn the Invocations pattern from these samples, then adapt the relevant Responses sample — the capability code is the same, only the HTTP handler differs.

| Sample | What it shows |
|--------|---------------|
| **[Echo Agent](agent-framework/invocations-echo-agent/)** | Minimal invocations agent that echoes the request back — shows the invocations handler pattern. |

---

## LangGraph samples

> **LangGraph is Python-only.** See the [Python LangGraph samples](../../python/hosted-agents/) for LangGraph support on Foundry. The deployment, observability, Teams publishing, and evaluation infrastructure is identical — only the agent code differs.

---

## Bring Your Own Framework samples

Already built an agent with your own .NET code? The protocol SDKs (`Azure.AI.AgentServer.Responses` / `Azure.AI.AgentServer.Invocations`) give you the hosted agent HTTP contract — they host the web server, expose the right endpoint, and handle request parsing — so you just plug in your agent logic. This is the recommended path for BYO to ensure your agent stays aligned with the platform contract as new endpoints are added. For lower-level control, the **Core adapter** (`Azure.AI.AgentServer.Core`) gives you managed hosting, OpenTelemetry tracing, and health endpoints, but you handle the protocol details yourself.

> **Note:** If you're using AutoGen or Semantic Kernel, you don't need BYO — Agent Framework supports them natively. See the [Agent Framework samples](#agent-framework-samples) instead.

### Responses protocol

| Sample | What it shows |
|--------|--------------|
| **[Hello World](bring-your-own/responses/HelloWorld/)** | Minimal agent — calls a Foundry model via the Responses API and returns the reply. The simplest possible BYO starting point. |
| **[Notetaking Agent](bring-your-own/responses/notetaking-agent/)** | Agent that takes and retrieves notes using a custom tool. |
| **[Background Agent](bring-your-own/responses/background-agent/)** | Long-running background processing with async execution. |

### Invocations protocol

| Sample | What it shows |
|--------|--------------|
| **[Hello World](bring-your-own/invocations/HelloWorld/)** | Minimal agent — arbitrary JSON in, streaming SSE out. The simplest possible BYO invocations starting point. |
| **[Notetaking Agent](bring-your-own/invocations/notetaking-agent/)** | Note-taking agent with the Invocations protocol. |
| **[Human-in-the-Loop](bring-your-own/invocations/human-in-the-loop/)** | Long-running agent that pauses for human approval before continuing. |

**Which approach?** Use the protocol SDKs (`Azure.AI.AgentServer.Responses` / `Azure.AI.AgentServer.Invocations`) — they work with any framework and keep you aligned with the platform contract. The **Core adapter** (`Azure.AI.AgentServer.Core`) is available when you need lower-level control without protocol abstractions. The Custom HTTP sample exists as a reference for what the contract looks like under the hood with no SDK at all.

**What's different from Agent Framework samples:** BYO samples handle their own session state and tool wiring. The protocol SDKs give you the HTTP plumbing, but the tradeoff vs. full Agent Framework is that you manage orchestration, tools, and memory in your own code. The Dockerfile, agent.yaml, and `azd up` deployment are the same.

---

## Quick reference

| Capability | Sample (Responses) | Sample (Invocations) |
|------------|-------------------|---------------------|
| Deploy and invoke a hosted agent | [Hello World](agent-framework/hello-world/) | [Echo Agent](agent-framework/invocations-echo-agent/) |
| Multi-turn conversation with session persistence | [Multi-Turn Sessions](agent-framework/simple-agent/) | — |
| Streaming | [Hello World](agent-framework/hello-world/) (built-in) | — |
| Local function tools | [Tools](agent-framework/local-tools/) | — |
| RAG / knowledge grounding | [Knowledge Grounding](agent-framework/text-search-rag/) | — |
| Multi-agent workflow | [Workflows](agent-framework/workflows/) | — |
| BYO agent (any framework) | [BYO Hello World](bring-your-own/responses/HelloWorld/) | [BYO Hello World](bring-your-own/invocations/HelloWorld/) |
| Observability (App Insights, OpenTelemetry, traces) | Every sample — enabled by default | Every sample — enabled by default |

## Deploy any sample

Every sample deploys the same way. You need the [Azure Developer CLI (azd)](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) and a Foundry project with a model deployment.

```bash
mkdir my-agent && cd my-agent

# Scaffold from the sample manifest — azd generates all the deployment files
azd ai agent init -m ../agent-framework/hello-world/agent.manifest.yaml

# Build, push, and deploy
azd up

# Clean up when done
azd down
```

### Other ways to invoke your agent

| Method | When to use |
|--------|------------|
| `azd ai agent invoke` | Quick CLI test after deploy |
| [VS Code Foundry extension](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=vscode) | One-click invoke from the editor |
| `curl` | Each sample README includes curl examples |

## Repo structure

```
samples/dotnet/hosted-agents/
├── agent-framework/
│   ├── hello-world/                   ← Start here (Agent Framework)
│   ├── simple-agent/
│   ├── local-tools/
│   ├── mcp-tools/
│   ├── text-search-rag/
│   ├── workflows/
│   └── invocations-echo-agent/
└── bring-your-own/
    ├── responses/
    │   ├── HelloWorld/                ← Start here (BYO Responses)
    │   ├── notetaking-agent/
    │   └── background-agent/
    └── invocations/
        ├── HelloWorld/                ← Start here (BYO Invocations)
        ├── notetaking-agent/
        └── human-in-the-loop/
```

### Language and framework coverage

| Framework | Protocol | C# | Python |
|-----------|----------|----|--------|
| **Agent Framework** | Responses | 5 samples | 3 samples |
| **Agent Framework** | Invocations | 1 sample (echo agent) | — |
| **LangGraph** | — | — (Python-only) | See [Python README](../../python/hosted-agents/) |
| **Bring Your Own** | Responses | 3 samples | 5 samples |
| **Bring Your Own** | Invocations | 3 samples | 7 samples |

The LangGraph samples are Python-only because LangGraph is a Python-native framework, but the containerized deployment pattern is identical.

## What's in every sample

```
<sample-name>/
├── README.md               # What it does, prerequisites, deploy, invoke, clean up
├── Program.cs              # Agent entry point (with OpenTelemetry + App Insights init)
├── <ProjectName>.csproj    # Project file with NuGet dependencies
├── Dockerfile              # Container definition (port 8088, .NET 10 multi-stage build)
├── .dockerignore
├── agent.manifest.yaml     # Agent definition — name, protocols, environment variables
└── agent.yaml              # Deployed agent config — protocol, resources
```

Python samples follow the same layout with `main.py`, `requirements.txt`, and a Python-based Dockerfile.

> Samples do not include `azure.yaml`. The `azd ai agent init -m agent.manifest.yaml` command generates the project configuration automatically from the agent manifest.

## Prerequisites

- **Azure subscription** with access to Microsoft Foundry
- **Azure Developer CLI (azd)** — [install](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd)
- **.NET 10** (or **Python 3.12+** for Python samples)

That's it. `azd ai agent init` and the VS Code Foundry extension will create a Foundry project and deploy a model for you if you don't already have one. Container images are built remotely using ACR Tasks by default — **Docker is not required** unless you want to build locally.

## Resources

- [Microsoft Foundry documentation](https://learn.microsoft.com/en-us/azure/foundry/what-is-foundry?view=foundry)
- [Hosted agents overview](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents?view=foundry)
- [Deploy a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/deploy-hosted-agent)
- **Responses protocol:** [Python SDK (`azure-ai-agentserver-responses`)](https://pypi.org/project/azure-ai-agentserver-responses/) · [C# SDK (`Azure.AI.AgentServer.Responses`)](https://www.nuget.org/packages/Azure.AI.AgentServer.Responses)
- **Invocations protocol:** [Python SDK (`azure-ai-agentserver-invocations`)](https://pypi.org/project/azure-ai-agentserver-invocations/) · [C# SDK (`Azure.AI.AgentServer.Invocations`)](https://www.nuget.org/packages/Azure.AI.AgentServer.Invocations)
- **Core adapter (BYO):** [Python SDK (`azure-ai-agentserver-core`)](https://pypi.org/project/azure-ai-agentserver-core/) · [C# SDK (`Azure.AI.AgentServer.Core`)](https://www.nuget.org/packages/Azure.AI.AgentServer.Core)
- [Azure Developer CLI (azd)](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)

## Contributing

This project welcomes contributions and suggestions.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.
