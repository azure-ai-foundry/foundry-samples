# Copyright (c) Microsoft. All rights reserved.

"""Synthetic end-to-end test for the Voice Live hello-world agent.

Connects to ``ws://<host>:<port>/invocations_ws`` and sends a JSON text
message (``{"type":"text","content":"..."}``). This bypasses Voice Live's
server-VAD so the test does not need a real spoken utterance. Asserts:

  * a ``session_started`` JSON event arrives;
  * at least one binary audio frame is returned (assistant speech);
  * a ``response_done`` event arrives;
  * the connection closes cleanly.

Run::

    python e2e_local.py                # against ws://localhost:8088
    python e2e_local.py --url ws://...

Requires the agent process to be running and the standard
``AZURE_VOICELIVE_*`` env vars to be set so the agent can reach Voice Live.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import struct
import subprocess
import sys
import time
from urllib.parse import quote, urlencode, urlsplit, urlunsplit

import websockets


def _foundry_url(project_endpoint: str, agent: str, session_id: str, api_version: str = "v1") -> str:
    parts = urlsplit(project_endpoint)
    project = parts.path.rstrip("/").rsplit("/", 1)[-1]
    qs = urlencode({
        "api-version": api_version,
        "agent_session_id": session_id,
    })
    path = (
        f"/api/projects/{quote(project, safe='')}"
        f"/agents/{quote(agent, safe='')}"
        "/endpoint/protocols/invocations_ws"
    )
    return urlunsplit((
        "wss" if parts.scheme in ("https", "wss") else "ws",
        parts.netloc,
        path,
        qs, "",
    ))


def _entra_token(resource: str = "https://ai.azure.com") -> str:
    out = subprocess.run(
        ["az", "account", "get-access-token", "--resource", resource, "-o", "json"],
        capture_output=True, text=True, check=True,
    ).stdout
    return json.loads(out)["accessToken"]


async def _run(
    url: str,
    timeout: float,
    prompt: str,
    headers: dict[str, str] | None = None,
    idle: bool = False,
) -> int:
    print(f"[e2e] connecting {url} ...", flush=True)

    got_session = False
    got_audio_bytes = 0
    response_done_count = 0
    got_error: str | None = None

    deadline = time.monotonic() + timeout

    async with websockets.connect(
        url,
        max_size=4 * 1024 * 1024,
        additional_headers=list((headers or {}).items()) or None,
    ) as ws:
        async def sender():
            # Wait until the session is ready, then send a text message.
            for _ in range(100):
                if got_session:
                    break
                await asyncio.sleep(0.05)
            payload = json.dumps({"type": "text", "content": prompt})
            print(f"[e2e] -> text: {prompt!r}", flush=True)
            await ws.send(payload)

        # In --idle mode we send nothing and just wait for the agent's
        # proactive greeting followed by an idle re-engagement response.
        send_task = None if idle else asyncio.create_task(sender())

        try:
            while time.monotonic() < deadline:
                try:
                    raw = await asyncio.wait_for(ws.recv(), timeout=deadline - time.monotonic())
                except asyncio.TimeoutError:
                    break
                if isinstance(raw, (bytes, bytearray)):
                    if len(raw) > 8:
                        sr, ch = struct.unpack("<II", raw[:8])
                        got_audio_bytes += len(raw) - 8
                        if got_audio_bytes <= 4096:
                            print(f"[e2e] audio frame sr={sr} ch={ch} +{len(raw)-8}B (total {got_audio_bytes}B)", flush=True)
                else:
                    try:
                        msg = json.loads(raw)
                    except Exception:
                        print(f"[e2e] non-json text: {raw!r}", flush=True)
                        continue
                    print(f"[e2e] event: {msg}", flush=True)
                    if msg.get("type") == "session_started":
                        got_session = True
                    elif msg.get("type") == "error":
                        got_error = str(msg)
                    elif msg.get("type") == "response_done":
                        response_done_count += 1
                        # Idle mode needs greeting + re-engagement = 2.
                        target = 2 if idle else 1
                        if response_done_count >= target and got_audio_bytes > 0:
                            break
        finally:
            if send_task is not None:
                send_task.cancel()
                try:
                    await send_task
                except (asyncio.CancelledError, Exception):
                    pass

    print()
    print(f"[e2e] session_started:    {got_session}")
    print(f"[e2e] audio_bytes recvd:  {got_audio_bytes}")
    print(f"[e2e] response_done seen: {response_done_count}")
    print(f"[e2e] error:              {got_error}")

    min_responses = 2 if idle else 1
    ok = (
        got_session
        and got_audio_bytes > 0
        and response_done_count >= min_responses
        and got_error is None
    )
    print(f"[e2e] result:             {'PASS' if ok else 'FAIL'}")
    return 0 if ok else 1


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--url", default=None,
                   help="Override WebSocket URL. Default: ws://localhost:8088/invocations_ws "
                        "or, if --foundry/--agent given, the Foundry public URL.")
    p.add_argument("--timeout", type=float, default=45.0,
                   help="Hard timeout in seconds for the test.")
    p.add_argument("--prompt", default="Say hello in one short sentence.",
                   help="Text prompt to send to the agent.")
    p.add_argument("--foundry", default=None,
                   help="Foundry project endpoint, e.g. "
                        "'https://<acct>.services.ai.azure.com/api/projects/<proj>'. "
                        "When set, builds the public WS URL and sends an Entra Bearer token.")
    p.add_argument("--agent", default="hello-world",
                   help="Hosted agent name (Foundry mode only).")
    p.add_argument("--idle", action="store_true",
                   help="Idle re-engagement test: send no input and assert "
                        "a second response_done arrives after the proactive "
                        "greeting (requires AZURE_VOICELIVE_IDLE_ENGAGEMENT_"
                        "SECONDS to be set small on the agent).")
    args = p.parse_args()

    headers: dict[str, str] = {}
    if args.foundry:
        sid = f"e2e-{int(time.time())}"
        url = args.url or _foundry_url(args.foundry.rstrip("/"), args.agent, sid)
        token = _entra_token()
        headers = {
            "Authorization": f"Bearer {token}",
            "Foundry-Features": "HostedAgents=V1Preview",
        }
    else:
        url = args.url or "ws://localhost:8088/invocations_ws"

    return asyncio.run(_run(url, args.timeout, args.prompt, headers, idle=args.idle))


if __name__ == "__main__":
    sys.exit(main())
