# Copyright (c) Microsoft. All rights reserved.

"""Diagnostic Agent — network and environment diagnostics from inside a
hosted-agent runtime sandbox.

On each invocation this thin handler builds a :class:`ProbeContext` from the
request, lets the :mod:`runner` execute every registered probe, and returns a
single structured JSON response assembled by :mod:`report`. It answers:

    "What can the runtime inside the delegated agent subnet actually reach,
     and if a name won't resolve or a host won't connect, where does it break?"

Architecture (see ``DEVELOPING_PROBES.md``):

    request → ProbeContext.from_spec → runner.run_all(registry) → report.build_report → JSON

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
      "direct_targets":   ["10.0.1.9:443"],        // reachability WITHOUT DNS
      "resolvers":        ["168.63.129.16"],        // extra DNS servers to compare
      "record_types":     ["A", "AAAA"],
      "raw_dns":          true,                     // automate dig per resolver x type
      "dns_attempts":     20,                       // repeat each query -> intermittency
      "gai_attempts":     20,                       // repeat getaddrinfo -> OS failure rate
      "parallel_probe":   true,                     // glibc-style parallel A+AAAA on one socket
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

import json
import logging
import os
import sys
import time
import traceback
from datetime import datetime, timezone

from starlette.requests import Request
from starlette.responses import JSONResponse

from azure.ai.agentserver.invocations import InvocationAgentServerHost

import probes  # noqa: F401 — importing registers all built-in probes
from framework import report, runner
from framework.context import ProbeContext

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


@app.invoke_handler
async def handle_invoke(request: Request) -> JSONResponse:
    session_id = getattr(request.state, "session_id", None)
    invocation_id = getattr(request.state, "invocation_id", None)
    t_start = time.perf_counter()

    try:
        body = await request.body()
        spec = _parse_body(body)
        ctx = ProbeContext.from_spec(spec)

        logger.info(
            "invoke start invocation=%s session=%s body_len=%d hosts=%d public=%d",
            invocation_id, session_id, len(body), len(ctx.hosts), len(ctx.public_hosts),
        )
        logger.debug("invoke spec=%s", spec)

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
        return JSONResponse(body_out)
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


if __name__ == "__main__":
    app.run()
