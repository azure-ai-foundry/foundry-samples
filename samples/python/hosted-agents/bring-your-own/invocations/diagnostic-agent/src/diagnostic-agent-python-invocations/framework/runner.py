# Copyright (c) Microsoft. All rights reserved.

"""Runs the registered probes and returns a flat list of ``ProbeResult``.

The runner is deliberately probe-agnostic (it knows nothing about DNS, TLS, etc.)
so adding a probe never touches this file:

1. Select the probes whose ``applies_to(ctx)`` is true.
2. Call each selected probe's optional ``pre_snapshot(ctx)`` — lets a probe
   capture a baseline (e.g. NIC/UDP counters) before any diagnostic work runs, so
   its ``run`` can report a delta bracketing the whole pass.
3. Run each probe under its own try/except. A crash is converted to a single
   ``status="error"`` result (isolation) and never aborts sibling probes.
"""

from __future__ import annotations

import logging
import time
import traceback

from framework.context import ProbeContext
from framework.contract import ProbeResult, error_result
from framework.registry import all_probes

logger = logging.getLogger("diagnostic_agent.runner")


def run_all(ctx: ProbeContext) -> list[ProbeResult]:
    selected = []
    for probe in all_probes():
        try:
            if probe.applies_to(ctx):
                selected.append(probe)
        except Exception:  # noqa: BLE001 — a bad applies_to must not abort selection
            logger.exception("applies_to failed for probe %s", getattr(probe, "id", "?"))

    # Pass 1: baselines (bracket the whole diagnostic pass for delta metrics).
    for probe in selected:
        pre = getattr(probe, "pre_snapshot", None)
        if pre is None:
            continue
        try:
            pre(ctx)
        except Exception:  # noqa: BLE001 — a baseline failure is non-fatal
            logger.exception("pre_snapshot failed for probe %s", getattr(probe, "id", "?"))

    # Pass 2: run.
    results: list[ProbeResult] = []
    for probe in selected:
        pid = getattr(probe, "id", "?")
        pver = getattr(probe, "version", 0)
        t0 = time.perf_counter()
        try:
            produced = probe.run(ctx) or []
            if not isinstance(produced, list) or any(not isinstance(item, ProbeResult) for item in produced):
                raise TypeError(f"Probe '{pid}' must return list[ProbeResult]")
            results.extend(produced)
            logger.info(
                "probe %s produced=%d ms=%.1f",
                pid,
                len(produced),
                (time.perf_counter() - t0) * 1000,
            )
        except Exception as e:  # noqa: BLE001 — isolate: one probe crash never aborts the rest
            tb = traceback.format_exc()
            logger.error("probe %s FAILED err=%s\n%s", pid, type(e).__name__, tb)
            results.append(error_result(pid, pver, {}, e, traceback_str=tb))
    return results
