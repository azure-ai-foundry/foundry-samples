---
name: azure-playwright-browser-automation
description: Automates browser interactions for web testing, form filling, screenshots, and data extraction using Playwright CLI connected to a remote Azure Playwright Service browser.
allowed-tools: run_playwright_cli, create_session, close_browser_session
---

# Browser automation with Playwright CLI and Azure Playwright Service

This skill is the operational reference for browser tasks. The base prompt owns
the non-negotiable lifecycle rules; this skill provides the concrete Playwright
CLI command patterns.

## Remote browser connection

1. Reuse an active browser session for follow-up browser work in the same hosted
   agent session whenever one is available. If no active browser is available,
   call `create_session` with no arguments.
2. As soon as `create_session` returns, before calling `run_playwright_cli` or
   doing any other automation work, inspect the tool result for `liveViewUrl`.
   If the result includes `liveViewUrl`, emit this exact markdown message using
   the `liveViewUrl` value returned by the tool:

   ```text
   Created a new browser session [Live View URL](<liveViewUrl>)
   ```

   If the result does not include `liveViewUrl`, immediately emit this exact
   message before calling `run_playwright_cli`:

   ```text
   No liveViewUrl was returned from the tool call. Automation will still continue
   ```

   Do not derive or invent a live-view URL from `cdpUrl`. Do not include the raw
   CDP URL in user-facing text.
   The live-view dashboard URL is safe to share with the user; only the raw
   `cdpUrl` is sensitive. If a browser session was created in this turn, repeat
   the live-view markdown link in the final answer as well when `liveViewUrl`
   was returned. If the user asks for the live URL, provide the live-view
   markdown link directly when it is available; do not refuse and do not say you
   can generate it later.
3. Use local Playwright CLI `sessionId` `browser1`, then call
   `run_playwright_cli` with that `sessionId`, the returned `cdpUrl`, and the
   command:

   ```text
   open about:blank
   ```

   The tool sets `PLAYWRIGHT_MCP_CDP_ENDPOINT=<cdpUrl>` before invoking
   `playwright-cli -s=<sessionId> open about:blank`.

   This must be a standalone handshake command. Do not combine it with target
   navigation, `eval`, `snapshot`, or any other browser operation.
4. Run all subsequent commands with the same local Playwright CLI `sessionId`
   (`browser1` by default) and no `cdpUrl`:

   ```text
   goto https://example.com
   snapshot
   ```

   Use `goto <url>` for target navigation after the handshake; do not use
   `open <url>` for normal page navigation.
   If a follow-up command says the local session is not open, reconnect to the
   same remote browser with `command: open` and the stored `cdpUrl`, then retry
   the requested browser command. Do not call `create_session` for this recovery.
5. Keep the browser session open after successful work so follow-up tasks can
   continue in the same live browser. Call `close_browser_session` only when the
   user explicitly asks to close the browser, when the session is unusable, or
   before replacing it with a fresh remote browser.

If the initial `open about:blank` command with `cdpUrl` fails, do not retry the
same CDP URL repeatedly. Call `close_browser_session`, then call
`create_session` again to create a fresh remote browser.

## Installation check

The hosted container installs `@playwright/cli` and the packaged Playwright CLI
skills at build time:

```bash
npm install -g @playwright/cli@latest
playwright-cli install --skills
```

If running locally, verify the CLI before browser work:

```bash
playwright-cli --help
```

## Common commands

Pass only the arguments shown below to `run_playwright_cli.command`; do not
include `playwright-cli` or `-s=<sessionId>`.

```bash
# Navigation
open
open https://example.com
goto https://playwright.dev
go-back
go-forward
reload

# Page state
snapshot
snapshot --filename=after-click.yaml
snapshot --depth=4
screenshot
screenshot --filename=page.png

# Interactions
click e3
dblclick e7
fill e5 "user@example.com"
fill e5 "search text" --submit
type "search query"
press Enter
hover e4
select e9 "option-value"
check e12
uncheck e12

# Tabs
tab-list
tab-new https://example.com/other
tab-select 0
tab-close

# Extraction and diagnostics
eval "document.title"
eval "JSON.stringify([...document.querySelectorAll('a')].map(a => a.href))"
console
requests
request 5
```

After most commands, Playwright CLI emits page status and a snapshot. Use refs
from the snapshot, such as `e15`, for subsequent interactions.

Use `--raw` inside the command when you need only a result value:

```text
--raw eval "document.title"
```

## Cleanup

Do not close a healthy remote browser at the end of a normal task. When the user
asks to close the browser, or when replacing a broken session, call
`close_browser_session` with:

```json
{ "sessionId": "<sessionId>", "cdpUrl": "<cdpUrl>" }
```

This detaches Playwright CLI from the held WSS/CDP connection for the named
session, then closes the remote browser over CDP.
