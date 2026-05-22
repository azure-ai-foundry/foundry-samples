# Sample structure

This sample demonstrates a Foundry-hosted browser automation agent that can be
specialized without duplicating runtime code.

## Design goals

- Keep one shared implementation for Foundry hosting, tools, Toolbox MCP wiring,
  Playwright CLI execution, logging, and cleanup.
- Keep browser lifecycle invariants in one shared base prompt.
- Let users tailor the agent by editing small profile prompt files.
- Keep skills for concrete operational references rather than broad personas.

## Layers

| Layer | Path | Purpose |
| --- | --- | --- |
| Runtime code | `src/browser_automation_agent_sample_foundry/` | Builds the Agent Framework agent, hosts Responses, wires tools, reads prompts, and logs tool use. |
| Base prompt | `prompts/base.md` | Non-negotiable lifecycle, tool, safety, and cleanup rules. |
| Profiles | `prompts/profiles/*.md` | Task-specific behavior for general automation, scraping, form filling, and QA testing. |
| Skill | `skills/azure-playwright-browser-automation/SKILL.md` | Operational Playwright CLI reference for Azure Playwright Service sessions. |
| Toolbox MCP | Foundry Toolbox | Governed remote MCP endpoint that provides `create_session`. |
| Deployment | `agent.yaml`, `agent.manifest.yaml`, `azure.yaml`, `Dockerfile` | Foundry hosted-agent and container configuration. |

The Docker image installs `@playwright/cli` and runs
`playwright-cli install --skills`. The sample also keeps an Agent Framework skill
under `skills/` so the hosted agent has explicit instructions for the Azure
Playwright Service lifecycle.

`agent.manifest.yaml` is the source manifest used by `azd ai agent init`.
`agent.yaml` is the generated hosted-agent definition used by deployment. Keep
model defaults, environment variables, and resource settings aligned if you edit
both files.

## Prompt composition

At startup, the agent reads:

```text
prompts/base.md
+
prompts/profiles/<BROWSER_AGENT_PROFILE>.md
```

`BROWSER_AGENT_PROFILE` defaults to `general`. Supported sample profiles:

- `general`
- `web-scraper`
- `form-filler`
- `qa-tester`

For a fully custom prompt, set `BROWSER_AGENT_PROMPT_FILE` to a Markdown file
path. The base prompt is still included first so lifecycle and safety rules
remain intact.

## Why profiles instead of separate agents?

Separate projects for a web scraper agent and a form filler agent would duplicate
the same Foundry hosting code, Playwright CLI wrapper, Toolbox setup, Dockerfile,
and cleanup behavior. Profile files keep customization visible while avoiding
runtime drift.

## Why not generic scraping/form-filling skills?

Skills are most useful when they contain repeatable procedural knowledge. A
generic "web scraping" skill often becomes broad advice that is hard to maintain
and easy to overstate. This sample uses profiles for broad task style and keeps
the skill focused on the concrete browser automation workflow.

## Adding a new profile

1. Copy an existing profile:

   ```powershell
   Copy-Item prompts\profiles\web-scraper.md prompts\profiles\price-monitor.md
   ```

2. Edit the copied file with the desired behavior and output style.
3. Deploy or run with:

   ```powershell
   azd env set BROWSER_AGENT_PROFILE "price-monitor"
   ```

No Python code changes are required.

## Adding deeper domain behavior

If a use case has a real repeatable procedure, add a new skill under `skills/`.
Keep the base browser lifecycle rules in `prompts/base.md`; do not duplicate
them into every skill.

