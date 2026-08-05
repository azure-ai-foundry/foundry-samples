# Copyright (c) Microsoft. All rights reserved.

"""Diagnostic Agent — network and environment diagnostics from inside a
hosted-agent runtime sandbox.

On each invocation this thin handler builds a :class:`ProbeContext` from the
request, lets the :mod:`runner` execute every registered probe, and returns a
structured JSON response or an opt-in SSE stream assembled by :mod:`report`. It answers:

    "What can the runtime inside the delegated agent subnet actually reach,
     and if a name won't resolve or a host won't connect, where does it break?"

Architecture (see ``DEVELOPMENT.md`` at the sample root):

    request → ProbeContext.from_spec → runner.run_all(registry) → report.build_report → JSON/SSE

Every probe emits the same ``ProbeResult`` envelope, so adding a probe requires
no change here or in the runner/aggregator (Open/Closed). Probes are discovered
purely through the registry (``import probes`` registers the built-ins).

The probe code is deliberately **stdlib-only** (``socket``, ``ssl``, ``urllib``,
``http.client``) — the network is the very thing being diagnosed, so probes must
not depend on an import-time package fetch. No LLM and no Foundry project
endpoint are required.

POST body contract (all fields optional)::

    {
      "hosts":            ["<acr>.azurecr.io", "<acct>.services.ai.azure.com"],
      "public_hosts":     ["https://management.azure.com/"],
      "direct_targets":   ["10.0.1.9:443"],         // reachability WITHOUT DNS
      "resolvers":        ["168.63.129.16"],        // extra DNS servers to compare
      "record_types":     ["A", "AAAA"],
      "raw_dns":          true,                     // automate dig per resolver x type
      "dns_attempts":     20,                       // repeat each query -> intermittency
      "gai_attempts":     20,                       // repeat getaddrinfo -> OS failure rate
      "parallel_probe":   true,                     // glibc-style parallel A+AAAA on one socket
      "dns_propagation_probe":         true,        // time-spaced getaddrinfo sampling
      "dns_propagation_duration_sec":  30,
      "dns_propagation_interval_sec":  1,
      "dns_propagation_threshold_sec": 15,
      "keepalive_ping":    true,                    // cheap 2xx short-circuit; response also reports the self-ping loop's last status
      "stream":            true,                    // SSE heartbeats + final report
      "include_env_dump":        true,
      "include_container_info":  true,
      "include_evidence":        true,              // include verbose per-probe evidence
      "tcp_timeout_sec":  5,
      "http_timeout_sec": 10,
      "dns_timeout_sec":  5
    }

A plain-text body is treated as a single hostname. An empty body runs a default
profile (container + env + a few public Azure endpoints). The response is
**always HTTP 200** — every probe and the handler itself are isolated, so the
caller (which often cannot read non-2xx bodies) always gets actionable data.
"""

from __future__ import annotations

import http.client
import json
import logging
import os
import ssl
import sys
import threading
import time
import traceback
import urllib.parse
from datetime import datetime, timezone

from starlette.requests import Request
from starlette.responses import JSONResponse, Response, StreamingResponse

from azure.ai.agentserver.invocations import InvocationAgentServerHost

import probes  # noqa: F401 — importing registers all built-in probes
from framework import report, runner
from framework.context import ProbeContext
from framework.streaming import stream_report, wants_stream

logging.basicConfig(
    level=os.environ.get("DEBUG_AGENT_LOG_LEVEL", "INFO"),
    format="%(asctime)s %(levelname)s %(name)s %(message)s",
    stream=sys.stdout,
)
logging.getLogger().setLevel(os.environ.get("DEBUG_AGENT_LOG_LEVEL", "INFO"))
logger = logging.getLogger("diagnostic_agent")


def _parse_body(body: bytes) -> dict:
    if not body:
        return {}
    try:
        data = json.loads(body)
        return data if isinstance(data, dict) else {}
    except json.JSONDecodeError:
        # Plain-text body (e.g. the Foundry portal chat UI) — treat it as a
        # single hostname so users can paste an FQDN and get answers.
        text = body.decode("utf-8", errors="replace").strip()
        return {"hosts": [text]} if text else {}


app = InvocationAgentServerHost()


def _build_diagnostic_report(
    ctx: ProbeContext,
    session_id: str | None,
    invocation_id: str | None,
) -> dict:
    t_start = time.perf_counter()
    results = runner.run_all(ctx)
    elapsed_ms = round((time.perf_counter() - t_start) * 1000, 1)
    body_out = report.build_report(
        ctx, results, session_id=session_id, invocation_id=invocation_id, elapsed_ms=elapsed_ms,
    )
    summary = body_out.get("summary", {})
    logger.info(
        "invoke ok invocation=%s session=%s ms=%s results=%d verdict=%s errored=%s",
        invocation_id, session_id, elapsed_ms, len(results),
        summary.get("status"), summary.get("probes_errored"),
    )
    return body_out


@app.invoke_handler
async def handle_invoke(request: Request) -> Response:
    session_id = getattr(request.state, "session_id", None)
    invocation_id = getattr(request.state, "invocation_id", None)
    t_start = time.perf_counter()

    try:
        body = await request.body()
        spec = _parse_body(body)
        # Cheap keep-alive path: return 2xx before any probe work so the startup
        # self-ping loop generates inbound activity without cost or recursion. The
        # response also carries the loop's last self-ping status for observability.
        if spec.get("keepalive_ping"):
            return JSONResponse(
                {
                    "status": "alive",
                    "agent_session_id": session_id,
                    "invocation_id": invocation_id,
                    "keepalive_self_ping": _self_ping_snapshot(),
                }
            )
        ctx = ProbeContext.from_spec(spec)

        logger.info(
            "invoke start invocation=%s session=%s body_len=%d hosts=%d public=%d",
            invocation_id, session_id, len(body), len(ctx.hosts), len(ctx.public_hosts),
        )
        logger.debug("invoke spec=%s", spec)

        build_report = lambda: _build_diagnostic_report(ctx, session_id, invocation_id)
        if wants_stream(spec, request.headers.get("accept", "")):
            return StreamingResponse(
                stream_report(build_report, invocation_id),
                media_type="text/event-stream",
                headers={
                    "Cache-Control": "no-cache",
                    "Connection": "keep-alive",
                    "X-Accel-Buffering": "no",
                },
            )
        return JSONResponse(build_report())
    except Exception as e:  # noqa: BLE001 — last-chance; still return 200 with details
        elapsed_ms = round((time.perf_counter() - t_start) * 1000, 1)
        tb = traceback.format_exc()
        logger.error(
            "invoke FAIL invocation=%s session=%s ms=%s err=%s msg=%s\n%s",
            invocation_id, session_id, elapsed_ms, type(e).__name__, str(e)[:500], tb,
        )
        return JSONResponse(
            {
                "status": "handler_error",
                "agent_session_id": session_id,
                "invocation_id": invocation_id,
                "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                "elapsed_ms": elapsed_ms,
                "error": {"type": type(e).__name__, "message": str(e)[:500], "traceback": tb},
            }
        )


_KEEPALIVE_SELF_PING_SCOPE = "https://ai.azure.com/.default"
_KEEPALIVE_INVOCATIONS_ROUTE = "/endpoint/protocols/invocations"

# Last-known self-ping state, written by the loop and read by the keepalive_ping
# route so a caller can observe the loop without scraping container logs. When the
# loop is off, ``enabled``/``running`` stay False so the response says as much.
_self_ping_lock = threading.Lock()
_self_ping_state: dict = {
    "enabled": False,
    "running": False,
    "interval_sec": None,
    "duration_sec": None,
    "ping_count": 0,
    "last_code": None,
    "last_error": None,
    "last_ping_utc": None,
    "last_session_id": None,
}


def _self_ping_update(**fields) -> None:
    with _self_ping_lock:
        _self_ping_state.update(fields)


def _self_ping_snapshot() -> dict:
    with _self_ping_lock:
        return dict(_self_ping_state)


def _keepalive_acquire_token() -> str | None:
    """Fetch a data-plane token via the agent's own identity (managed/workload)."""
    try:
        from azure.identity import DefaultAzureCredential

        return DefaultAzureCredential().get_token(_KEEPALIVE_SELF_PING_SCOPE).token
    except Exception as e:  # noqa: BLE001 — surfaced as a skipped ping, not fatal
        logger.warning("keepalive self-ping: token acquisition failed: %s", e)
        return None


def _keepalive_self_ping(url: str, bearer: str, session_id: str, timeout: int) -> int:
    """Authenticated POST to this agent's own cheap ``keepalive_ping`` route."""
    parsed = urllib.parse.urlparse(url)
    tls = ssl.create_default_context()
    tls.minimum_version = ssl.TLSVersion.TLSv1_2
    conn = http.client.HTTPSConnection(
        parsed.hostname, parsed.port or 443, timeout=timeout, context=tls
    )
    path = f"{parsed.path}?{parsed.query}" if parsed.query else parsed.path
    payload = json.dumps({"keepalive_ping": True}).encode("utf-8")
    headers = {
        "Authorization": f"Bearer {bearer}",
        "Content-Type": "application/json",
        "Accept": "application/json",
        "x-agent-session-id": session_id,
    }
    conn.request("POST", path, body=payload, headers=headers)
    resp = conn.getresponse()
    resp.read(512)
    return resp.status


def _keepalive_self_ping_loop() -> None:
    """Keep this sandbox warm by self-invoking the cheap route on a fixed cadence.

    Runs only when ``KEEPALIVE_SELF_PING`` is set. It authenticates with the
    agent's own identity so the pings count as inbound activity, and re-pings on
    a fixed cadence for a bounded window, so a single deploy keeps the sandbox
    warm with no external caller.
    """
    interval = int(os.environ.get("KEEPALIVE_SELF_PING_INTERVAL_SEC", "120"))
    duration = int(os.environ.get("KEEPALIVE_SELF_PING_DURATION_SEC", "900"))  # 15 min default; 0 = container lifetime
    timeout = int(os.environ.get("KEEPALIVE_SELF_PING_TIMEOUT_SEC", "30"))
    api_version = os.environ.get("KEEPALIVE_SELF_PING_API_VERSION", "v1")
    project = (os.environ.get("FOUNDRY_PROJECT_ENDPOINT") or "").strip().rstrip("/")
    agent = (os.environ.get("FOUNDRY_AGENT_NAME") or "").strip()
    if not (project.startswith("https://") and agent):
        logger.warning(
            "keepalive self-ping: disabled, FOUNDRY_PROJECT_ENDPOINT/FOUNDRY_AGENT_NAME unset",
        )
        _self_ping_update(last_error="disabled: FOUNDRY_PROJECT_ENDPOINT/FOUNDRY_AGENT_NAME unset")
        return

    start = time.monotonic()
    i = 0
    logger.info(
        "keepalive self-ping: loop start agent=%s interval=%ds duration=%ds",
        agent, interval, duration,
    )
    _self_ping_update(running=True, interval_sec=interval, duration_sec=duration)
    while True:
        i += 1
        elapsed = int(time.monotonic() - start)
        session_id = (os.environ.get("FOUNDRY_AGENT_SESSION_ID") or "").strip()
        token = _keepalive_acquire_token() if session_id else None
        if token and session_id:
            url = (f"{project}/agents/{agent}{_KEEPALIVE_INVOCATIONS_ROUTE}"
                   f"?api-version={api_version}&agent_session_id={session_id}")
            try:
                code = _keepalive_self_ping(url, token, session_id, timeout)
                logger.info("keepalive self-ping[%d] t+%ds -> %s session=%s", i, elapsed, code, session_id)
                _self_ping_update(
                    ping_count=i, last_code=code, last_error=None,
                    last_ping_utc=datetime.now(timezone.utc).isoformat(), last_session_id=session_id,
                )
            except Exception as e:  # noqa: BLE001 — transient; keep looping
                logger.warning("keepalive self-ping[%d] t+%ds error: %s", i, elapsed, e)
                _self_ping_update(
                    ping_count=i, last_code=None, last_error=str(e)[:200],
                    last_ping_utc=datetime.now(timezone.utc).isoformat(), last_session_id=session_id,
                )
        else:
            logger.info(
                "keepalive self-ping[%d] t+%ds skipped token=%s session=%s",
                i, elapsed, bool(token), bool(session_id),
            )
            _self_ping_update(
                ping_count=i, last_code=None,
                last_error=f"skipped (token={bool(token)} session={bool(session_id)})",
                last_ping_utc=datetime.now(timezone.utc).isoformat(),
            )
        if duration and (time.monotonic() - start) >= duration:
            break
        time.sleep(interval)
    _self_ping_update(running=False)
    logger.info("keepalive self-ping: loop exit after %d pings", i)


def _maybe_start_keepalive_self_ping() -> None:
    if os.environ.get("KEEPALIVE_SELF_PING", "").strip().lower() in ("1", "true", "yes"):
        _self_ping_update(enabled=True)
        threading.Thread(
            target=_keepalive_self_ping_loop, name="keepalive-self-ping", daemon=True
        ).start()


_maybe_start_keepalive_self_ping()


if __name__ == "__main__":
    app.run()
