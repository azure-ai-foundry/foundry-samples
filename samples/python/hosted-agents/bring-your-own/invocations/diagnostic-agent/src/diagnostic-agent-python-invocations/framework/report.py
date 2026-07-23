# Copyright (c) Microsoft. All rights reserved.

"""Assembles the top-level response envelope from the probe results.

Two distinct "status" values, intentionally:

* top-level ``status`` — did the *agent* run cleanly? ``ok`` normally, ``partial``
  if any probe crashed (``error``). The response is always HTTP 200 regardless.
* ``summary.status`` — the *diagnostic verdict* across findings
  (``ok``/``warn``/``fail``).
"""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from framework import aggregator
from framework.context import ProbeContext
from framework.contract import SCHEMA_VERSION, ProbeResult, Status


def build_report(
    ctx: ProbeContext,
    results: list[ProbeResult],
    *,
    session_id: str | None,
    invocation_id: str | None,
    elapsed_ms: float,
) -> dict[str, Any]:
    any_error = any(r.status == Status.ERROR for r in results)

    return {
        "schema_version": SCHEMA_VERSION,
        "status": "partial" if any_error else "ok",
        "agent_session_id": session_id,
        "invocation_id": invocation_id,
        "timestamp_utc": datetime.now(timezone.utc).isoformat(),
        "elapsed_ms": elapsed_ms,
        "summary": aggregator.summarize(results),
        "results": [r.to_dict(include_evidence=ctx.include_evidence) for r in results],
    }
