# Sample structure

This sample demonstrates a Foundry-hosted browser automation agent with one
shared runtime and one shared prompt.

## Design goals

- Keep one shared implementation for Foundry hosting, tools, Toolbox MCP wiring,
  Playwright CLI execution, logging, and cleanup.
- Keep browser lifecycle, safety, web extraction, and form-filling guidance in
  one shared base prompt.
- Keep skills for concrete operational references rather than broad personas.

## Layers

| Layer | Path | Purpose |
| --- | --- | --- |
| Runtime code | `main.py`, `utils/` | Builds the Agent Framework agent, hosts Responses, wires tools, reads prompts, and logs tool use. |
| Base prompt | `prompts/base.md` | Browser lifecycle, tool, safety, cleanup, web extraction, and form-filling rules. |
| Skill | `skills/azure-playwright-browser-automation/SKILL.md` | Operational Playwright CLI reference for Azure Playwright Service sessions. |
| Toolbox MCP | Foundry Toolbox | Governed remote MCP endpoint that provides `create_session`. |
| Deployment | `agent.yaml`, `agent.manifest.yaml`, `Dockerfile` | Foundry hosted-agent, Playwright workspace connection, and container configuration. |

The Docker image installs `@playwright/cli` and runs
`playwright-cli install --skills`. The sample also keeps an Agent Framework skill
under `skills/` so the hosted agent has explicit instructions for the Azure
Playwright Service lifecycle.

`agent.manifest.yaml` is the source manifest used by `azd ai agent init`.
`agent.yaml` is the hosted-agent definition used by deployment and the Foundry
Toolkit. Keep model defaults, environment variables, and resource settings
aligned if you edit both files.

## Prompt composition

At startup, the agent reads:

```text
prompts/base.md
```

The base prompt includes the general browser automation, structured web
extraction, and form-filling guidance so the deployed sample behaves as one
browser automation agent without runtime prompt selection.

## Why not generic scraping/form-filling skills?

Skills are most useful when they contain repeatable procedural knowledge. A
generic "web scraping" skill often becomes broad advice that is hard to maintain
and easy to overstate. This sample keeps broad task guidance in the base prompt
and keeps the skill focused on the concrete browser automation workflow.

## Adding deeper domain behavior

If a use case has a real repeatable procedure, add a new skill under `skills/`.
Keep the base browser lifecycle rules in `prompts/base.md`; do not duplicate
them into every skill.
