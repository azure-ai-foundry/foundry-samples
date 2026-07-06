# Caller agent — A2A delegation

A Foundry-hosted [Agent Framework](https://github.com/microsoft/agent-framework) agent (Responses protocol) that acts as a friendly concierge: when the user asks a question a specialist can answer better, the caller delegates it to a remote agent over the [A2A protocol](https://a2a-protocol.org/latest/) and summarizes the result back.

For the full two-agent walkthrough that pairs this caller with the included [executor](https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/python/hosted-agents/agent-framework/a2a/01-delegation/executor), see the **[parent README](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/a2a/01-delegation/README.md)**.

## How it works

The caller reaches the executor through a Foundry **Toolbox** that exposes one `a2a_preview` tool, backed by a `RemoteA2A` project connection. Both are declared in [`azure.yaml`](azure.yaml) as `kind: connection` + `kind: toolbox` resources and created by `azd provision` (see [supported toolbox scenarios](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/SUPPORTED_TOOLBOX_SCENARIOS.md) #10). The caller looks the toolbox up at runtime by name (`TOOLBOX_NAME`).

At runtime, the caller opens the toolbox MCP HTTP endpoint (`{project_endpoint}/toolboxes/{name}/mcp?api-version=v1`) using `MCPStreamableHTTPTool` from the Agent Framework SDK and lets the model auto-discover the executor's skills from the agent card. See [`main.py`](src/agent-framework-a2a-caller-responses/main.py).

The agent uses `FoundryChatClient` for the model (Responses API on the project endpoint) and is hosted with `ResponsesHostServer` from the [Agent Framework Foundry hosting package](https://pypi.org/project/agent-framework-foundry-hosting/) on port `8088`.

## Running locally

Follow [Running the Agent Host Locally](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/README.md#running-the-agent-host-locally). The toolbox / A2A connection lives in Foundry, so a local run still talks to the same remote executor.

```bash
azd ai agent invoke --local "What is 15 multiplied by 23?"
```

## Deploying

See the [parent README](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/a2a/01-delegation/README.md) — deploy the executor and run `setup-a2a` first to enable incoming A2A, then `azd provision` + `azd deploy` here. When `azd ai agent init` prompts for `a2a_executor_endpoint`, paste the A2A URL `setup-a2a` printed.

## Verify the deployed agent

```bash
azd ai agent invoke "What is 17 times 23?"
```

The caller should delegate the question over A2A and return a short answer (e.g. `17 times 23 is 391.`).
