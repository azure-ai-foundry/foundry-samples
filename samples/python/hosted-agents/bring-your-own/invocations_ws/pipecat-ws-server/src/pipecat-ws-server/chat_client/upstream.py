# Copyright (c) Microsoft. All rights reserved.

"""Upstream URL + auth resolution for the pipecat-ws-server portal.

Picks between local-mode (no auth, custom WS URL) and Foundry-hosted mode
(public services.ai.azure.com endpoint, Entra Bearer token).
"""

from __future__ import annotations

import json
import logging
import os
import subprocess
import time
from urllib.parse import quote, urlencode, urlsplit, urlunsplit


logger = logging.getLogger("chat-client.upstream")

AGENT_ENV = "PIPECAT_WEBSOCKET_AGENT_NAME"
LOCAL_ENV = "PIPECAT_WEBSOCKET_LOCAL_URL"


def _project_endpoint() -> str:
    return os.environ.get("PROJECT_ENDPOINT", "").rstrip("/")


def _api_version() -> str:
    return os.environ.get("API_VERSION", "v1").strip()


def _build_url(agent_name: str, session_id: str) -> str:
    endpoint = _project_endpoint()
    if not endpoint:
        raise RuntimeError(
            "PROJECT_ENDPOINT is required for Foundry mode. "
            "Set it in chat_client/.env or use PIPECAT_WEBSOCKET_LOCAL_URL."
        )
    parts = urlsplit(endpoint)
    scheme = "wss" if parts.scheme in ("https", "wss") else "ws"
    project = parts.path.rstrip("/").rsplit("/", 1)[-1]
    qs = urlencode({
        "api-version": _api_version(),
        "agent_session_id": session_id,
    })
    path = (
        f"/api/projects/{quote(project, safe='')}"
        f"/agents/{quote(agent_name, safe='')}"
        "/endpoint/protocols/invocations_ws"
    )
    return urlunsplit((
        scheme,
        parts.netloc,
        path,
        qs,
        "",
    ))


def _get_token() -> str:
    """Fetch an Entra Bearer token via the Azure CLI."""
    result = subprocess.run(
        [
            "az", "account", "get-access-token",
            "--resource", "https://ai.azure.com",
            "-o", "json",
        ],
        capture_output=True, text=True, check=True,
    )
    return json.loads(result.stdout)["accessToken"]


def resolve() -> tuple[str, str, dict[str, str]]:
    """Return ``(upstream_url, session_id, headers)``.

    Local mode: ``(LOCAL_URL value, "", {})`` — no auth.
    Foundry mode: ``(public_url, generated_sid, {Authorization})``.
    """
    local = os.environ.get(LOCAL_ENV, "").strip()
    if local:
        logger.info("local mode -> %s", local)
        return local, "", {}

    agent = os.environ.get(AGENT_ENV, "").strip()
    if not agent:
        raise RuntimeError(
            f"{AGENT_ENV} is required for Foundry mode "
            f"(or set {LOCAL_ENV} for local mode)"
        )

    sid = f"s-{agent}-{int(time.time())}"
    url = _build_url(agent, sid)
    token = _get_token()
    logger.info("foundry mode session_id=%s", sid)
    return url, sid, {
        "Authorization": f"Bearer {token}",
    }
