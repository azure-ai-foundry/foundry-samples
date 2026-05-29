# Copyright (c) Microsoft. All rights reserved.

"""Toolbox MCP client for browser session lifecycle management."""

from __future__ import annotations

import json
import logging

import httpx

logger = logging.getLogger(__name__)

_TOOLBOX_FEATURES = "Toolboxes=V1Preview"


class ToolboxClient:
    """Lightweight MCP client for Foundry Toolbox browser session management."""

    def __init__(self, endpoint: str, token_provider):
        self.endpoint = endpoint
        self._get_token = token_provider
        self._session_id: str | None = None
        self._req_id = 0
        self._initialized = False

    def _headers(self) -> dict:
        h = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {self._get_token()}",
        }
        if _TOOLBOX_FEATURES:
            h["Foundry-Features"] = _TOOLBOX_FEATURES
        if self._session_id:
            h["mcp-session-id"] = self._session_id
        return h

    def _next_id(self) -> int:
        self._req_id += 1
        return self._req_id

    def initialize(self) -> str:
        """Send MCP initialize + initialized notification."""
        if self._initialized:
            return "already-initialized"
        with httpx.Client(timeout=60) as client:
            resp = client.post(
                self.endpoint,
                headers=self._headers(),
                json={
                    "jsonrpc": "2.0",
                    "id": self._next_id(),
                    "method": "initialize",
                    "params": {
                        "protocolVersion": "2024-11-05",
                        "capabilities": {},
                        "clientInfo": {"name": "browser-automation-agent", "version": "1.0.0"},
                    },
                },
            )
            resp.raise_for_status()
            sid = resp.headers.get("mcp-session-id")
            if sid and sid != "None":
                self._session_id = sid
            data = resp.json()
            client.post(
                self.endpoint,
                headers=self._headers(),
                json={"jsonrpc": "2.0", "method": "notifications/initialized"},
            )
            self._initialized = True
            return data.get("result", {}).get("serverInfo", {}).get("name", "unknown")

    def call_tool(self, name: str, arguments: dict) -> dict:
        """Call a Toolbox tool and return the parsed JSON result."""
        self.initialize()
        with httpx.Client(timeout=120) as client:
            resp = client.post(
                self.endpoint,
                headers=self._headers(),
                json={
                    "jsonrpc": "2.0",
                    "id": self._next_id(),
                    "method": "tools/call",
                    "params": {"name": name, "arguments": arguments},
                },
            )
            resp.raise_for_status()
            result = resp.json().get("result", {})
            content = result.get("content", [])
            for c in content:
                if isinstance(c, dict) and c.get("type") == "text" and c.get("text"):
                    try:
                        return json.loads(c["text"])
                    except (json.JSONDecodeError, TypeError):
                        return {"text": c["text"]}
            return result
