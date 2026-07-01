# Caller agent — A2A delegation (.NET)

A Foundry-hosted [Agent Framework](https://github.com/microsoft/agent-framework) agent (Responses protocol) that acts as a friendly concierge: when the user asks a question a specialist can answer better, the caller delegates it to a remote agent over the [A2A protocol](https://a2a-protocol.org/latest/) and summarizes the result back.

For the full two-agent walkthrough that pairs this caller with the included [executor](../executor/), see the **[parent README](../README.md)**.

## How it works

The caller reaches the executor through a Foundry **Toolbox** that exposes one `a2a_preview` tool, backed by a `RemoteA2A` project connection. Both are declared in [`agent.manifest.yaml`](agent.manifest.yaml) as `kind: connection` + `kind: toolbox` resources and created by `azd provision` (see [supported toolbox scenarios](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/SUPPORTED_TOOLBOX_SCENARIOS.md) #10). At startup the caller registers the toolbox by name (`TOOLBOX_NAME`) with `AddFoundryToolboxes`; the hosting layer connects to the toolbox's managed MCP proxy, discovers its tools, and injects them into every request as **server-side tools** — Foundry executes them on the agent's behalf through the Responses API, which means the agent's container never has to broker the MCP / A2A traffic itself.

See [`Program.cs`](Program.cs) for the wiring, and [Supported toolbox tools](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/toolbox/SUPPORTED_TOOLBOX_TOOLS.md#a2a-tool-preview) for the `a2a_preview` parameters.

The agent uses `AsAIAgent` from the Foundry SDK (`Azure.AI.Projects`) for the model (Responses API on the project endpoint) and is hosted with `AgentHost.CreateBuilder` + `AddFoundryResponses` from the [Agent Framework Foundry hosting package](https://www.nuget.org/packages/Microsoft.Agents.AI.Foundry.Hosting) on port `8088`.

## Running locally

Follow [Running the Agent Host Locally](../../../README.md#running-the-agent-host-locally). The toolbox / A2A connection lives in Foundry, so a local run still talks to the same remote executor.

```bash
azd ai agent invoke --local "What is 15 multiplied by 23?"
```

## Deploying

See the [parent README](../README.md) — deploy the executor and run `setup-a2a` first to enable incoming A2A, then `azd provision` + `azd deploy` here. When `azd ai agent init` prompts for `a2a_executor_endpoint`, paste the A2A URL `setup-a2a` printed.

## Verify the deployed agent

```bash
azd ai agent invoke "What is 17 times 23?"
```

The caller should delegate the question over A2A and return a short answer (e.g. `17 times 23 is 391.`).
