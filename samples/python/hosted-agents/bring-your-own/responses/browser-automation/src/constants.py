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
