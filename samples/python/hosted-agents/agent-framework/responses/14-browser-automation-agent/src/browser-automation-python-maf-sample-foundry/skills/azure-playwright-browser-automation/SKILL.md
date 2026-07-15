---
name: azure-playwright-browser-automation
description: Automates browser interactions for web testing, form filling, screenshots, and data extraction using Playwright CLI connected to a remote Azure Playwright Service browser.
allowed-tools: run_playwright_cli, create_session, close_browser_session, get_live_view_url
---

# Browser automation with Playwright CLI and Azure Playwright Service

This skill is the operational reference for browser tasks. The base prompt owns
the non-negotiable lifecycle rules; this skill provides the concrete Playwright
CLI command patterns.

## Remote browser connection

1. Reuse an active browser session for follow-up browser work in the same hosted
   agent session whenever one is available. If no active browser is available,
   call `create_session` with no arguments.
2. As soon as `create_session` returns, call `get_live_view_url`. The live view
   URL will be delivered to the user automatically by the system. Do NOT output,
   repeat, or retype any URL yourself. Just acknowledge that the live view is
   available.

   Do not include the raw CDP URL in user-facing text.
3. Call `run_playwright_cli` with `sessionId='browser'` and the command:

   ```text
   open about:blank
   ```

   The CDP URL is injected automatically by the server -- do NOT pass it.
   This must be a standalone handshake command. Do not combine it with target
   navigation, `eval`, `snapshot`, or any other browser operation.
4. Run all subsequent commands with the same `sessionId` (`browser`):

   ```text
   goto https://example.com
   snapshot
   ```

   Use `goto <url>` for target navigation after the handshake; do not use
   `open <url>` for normal page navigation.
   If a follow-up command says the local session is not open, reconnect with
   `command: open about:blank`. Do not call `create_session` for this recovery.
5. Keep the browser session open after successful work so follow-up tasks can
   continue in the same live browser. Call `close_browser_session` only when the
   user explicitly asks to close the browser, when the session is unusable, or
   before replacing it with a fresh remote browser.

If the initial `open about:blank` command fails, do not retry repeatedly. Call
`close_browser_session`, then call `create_session` again to create a fresh
remote browser.

## Common commands

Pass only the arguments shown below to `run_playwright_cli.command`; do not
include `playwright-cli` or `-s=<sessionId>`.

```bash
# Navigation
open about:blank
goto https://playwright.dev
go-back
go-forward
reload

# Page state
snapshot
screenshot

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

# Date/calendar fields - use fill with the value format the field expects
fill e8 "2026-06-15"
fill e8 "06/15/2026"

# Tabs
tab-list
tab-new https://example.com/other
tab-select 0
tab-close

# Extraction and diagnostics
eval "document.title"
eval "JSON.stringify([...document.querySelectorAll('a')].map(a => a.href))"
console
```

After most commands, Playwright CLI emits page status and a snapshot. Use refs
from the snapshot, such as `e15`, for subsequent interactions.

## Cleanup

Do not close a healthy remote browser at the end of a normal task. When the user
asks to close the browser, or when replacing a broken session, call
`close_browser_session` with just `sessionId='browser'`.