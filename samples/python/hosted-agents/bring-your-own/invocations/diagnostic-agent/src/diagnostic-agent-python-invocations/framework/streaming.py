# Copyright (c) Microsoft. All rights reserved.

"""SSE framing and non-blocking execution for streamed diagnostics."""

from __future__ import annotations

import asyncio
import json
import time
from collections.abc import AsyncIterator, Callable
from typing import Any


def wants_stream(spec: dict[str, Any], accept_header: str = "") -> bool:
    return bool(spec.get("stream", False)) or "text/event-stream" in accept_header.lower()


def sse_event(event: dict[str, Any]) -> str:
    return f"data: {json.dumps(event, separators=(',', ':'))}\n\n"


async def stream_report(
    build_report: Callable[[], dict[str, Any]],
    invocation_id: str | None,
    heartbeat_sec: float = 5.0,
) -> AsyncIterator[str]:
    started = time.monotonic()
    task = asyncio.create_task(asyncio.to_thread(build_report))
    yield sse_event({"type": "started", "invocation_id": invocation_id})

    while True:
        done, _ = await asyncio.wait({task}, timeout=heartbeat_sec)
        if task not in done:
            yield sse_event(
                {
                    "type": "heartbeat",
                    "invocation_id": invocation_id,
                    "elapsed_sec": round(time.monotonic() - started, 1),
                }
            )
            continue

        try:
            report = task.result()
        except Exception as exc:  # noqa: BLE001 — response headers are already sent
            yield sse_event(
                {
                    "type": "error",
                    "invocation_id": invocation_id,
                    "error": {"type": type(exc).__name__, "message": str(exc)[:500]},
                }
            )
        else:
            yield sse_event({"type": "report", "report": report})
        yield sse_event({"type": "done", "invocation_id": invocation_id})
        return