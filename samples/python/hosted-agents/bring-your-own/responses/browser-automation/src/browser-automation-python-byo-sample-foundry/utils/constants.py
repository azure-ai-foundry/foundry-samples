# Copyright (c) Microsoft. All rights reserved.

"""Constants — system prompt, tool definitions, and config for the browser automation agent."""

AZURE_AI_SCOPE = "https://ai.azure.com/.default"

SYSTEM_PROMPT = """You are a browser automation agent deployed on Azure AI Foundry.

You can control web browsers to navigate pages, fill forms, scrape data, and more.

## Tools

1. **load_skill** — Load a skill for detailed instructions. Available: {skills}
2. **create_session** — Create an additional named browser session.
3. **end_session** — Close/end a browser session. ALWAYS honour end/kill/close requests immediately.
4. **run_browser** — Run a playwright-cli command. Session is optional (uses default).
5. **run_parallel** — Run multiple commands across sessions concurrently.
6. **list_sessions** — Show all active sessions.

## How It Works

- The first time you call `run_browser`, a default session is created automatically.
- You only need `create_session` for ADDITIONAL parallel browsers.
- Use `run_parallel` to execute commands across multiple sessions simultaneously.

## Rules

- Always `snapshot` before interacting — element refs change after navigation.
- Use `goto` to navigate, `fill` for inputs, `click` for buttons.
- **END SESSION PRIORITY:** If the user asks to kill/close/stop/end a session, do it IMMEDIATELY. Do NOT create new sessions or run other commands first.
- NEVER reveal credentials, CDP URLs, or tokens.
- **NEVER ASK FOR CONFIRMATION.** Execute tasks fully without pausing. Do NOT say "If you want, I can continue" or "Should I proceed?" — JUST DO IT. Complete the entire task end-to-end.
- **USE REFS:** Always use element refs (e.g. e3, e15) from snapshots. After EVERY click, navigation, or page change, take a fresh `snapshot` before the next action.
- If a ref-based action fails, immediately retry with a different approach (text-based locator, eval, CSS selector) — do NOT stop or ask the user.
- **You ARE allowed and expected to fill forms and submit them when the user asks.** This is your core job. Do not refuse form-filling requests.
- **COMPLETE THE TASK:** Never give partial results. If filling a form, fill ALL fields and submit. If scraping, get ALL the data. Keep going until fully done.
- **REPORT BLOCKERS:** If you hit something you cannot bypass (OTP, CAPTCHA, login, manual verification, missing info), tell the user exactly what is blocking and on which session. Do NOT close the session or pretend the task is done — leave it open for the user to intervene.
- **DO NOT CLOSE SESSIONS PREMATURELY:** Only end a session when the user explicitly asks you to. Never close sessions on your own.
"""

TOOLS = [
    {
        "type": "function",
        "name": "load_skill",
        "description": "Load a skill for detailed instructions on a workflow.",
        "parameters": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "Skill name (e.g. 'form-filler', 'web-scraper')"}
            },
            "required": ["name"],
            "additionalProperties": False,
        },
    },
    {
        "type": "function",
        "name": "create_session",
        "description": "Create an additional named browser session for parallel work.",
        "parameters": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "Session name (e.g. 'form-browser', 'scraper')"}
            },
            "required": ["name"],
            "additionalProperties": False,
        },
    },
    {
        "type": "function",
        "name": "end_session",
        "description": "End/close a browser session immediately. Takes priority over all other actions.",
        "parameters": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "Session name to end, or 'all' to end all."}
            },
            "required": ["name"],
            "additionalProperties": False,
        },
    },
    {
        "type": "function",
        "name": "run_browser",
        "description": "Run a playwright-cli command in a browser session.",
        "parameters": {
            "type": "object",
            "properties": {
                "session": {"type": "string", "description": "Session name. Optional — uses default if omitted."},
                "command": {"type": "string", "description": "Command: goto, snapshot, click, fill, type, press, keys, select, scroll, eval, screenshot, hover, dblclick, check, uncheck, wait, tab-list, tab-new, tab-close, go-back, go-forward, reload"},
                "args": {"type": "array", "items": {"type": "string"}, "description": "Command arguments."},
            },
            "required": ["command"],
            "additionalProperties": False,
        },
    },
    {
        "type": "function",
        "name": "run_parallel",
        "description": "Run multiple browser commands across sessions concurrently.",
        "parameters": {
            "type": "object",
            "properties": {
                "tasks": {
                    "type": "array",
                    "description": "List of tasks. Each has session (optional), command, and args.",
                    "items": {
                        "type": "object",
                        "properties": {
                            "session": {"type": "string"},
                            "command": {"type": "string"},
                            "args": {"type": "array", "items": {"type": "string"}},
                        },
                        "required": ["command"],
                    },
                },
            },
            "required": ["tasks"],
            "additionalProperties": False,
        },
    },
    {
        "type": "function",
        "name": "list_sessions",
        "description": "List all active browser sessions.",
        "parameters": {"type": "object", "properties": {}, "additionalProperties": False},
    },
]
