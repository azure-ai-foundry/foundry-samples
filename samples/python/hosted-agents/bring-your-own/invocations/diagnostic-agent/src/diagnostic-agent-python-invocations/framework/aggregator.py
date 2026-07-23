# Copyright (c) Microsoft. All rights reserved.

"""Probe-agnostic rollup: turn a flat list of ``ProbeResult`` into one ``summary``.

Because every probe emits the same envelope, this aggregator never needs to know
what any probe does — a newly contributed probe is summarized correctly the moment it is
registered, with zero changes here. This is the payoff of the uniform contract.
"""

from __future__ import annotations

from typing import Any

from framework.contract import (
    ProbeResult,
    Severity,
    Status,
    severity_rank,
    status_rank,
    worst_status,
)


def _target_label(target: dict[str, Any]) -> str | None:
    for key in ("host", "target", "url", "kind"):
        if target.get(key):
            return str(target[key])
    return None


def summarize(results: list[ProbeResult]) -> dict[str, Any]:
    worst = worst_status(results)

    findings_by_severity: dict[str, int] = {}
    flat_findings: list[tuple[int, dict[str, Any]]] = []
    targets_failed: list[str] = []
    probes_run: list[str] = []
    probes_errored: list[str] = []

    for r in results:
        if r.probe not in probes_run:
            probes_run.append(r.probe)
        if status_rank(r.status) >= status_rank(Status.FAIL):
            label = _target_label(r.target)
            if label and label not in targets_failed:
                targets_failed.append(label)
        if r.status == Status.ERROR and r.probe not in probes_errored:
            probes_errored.append(r.probe)
        for f in r.findings:
            sev = f.severity.value if isinstance(f.severity, Severity) else str(f.severity)
            findings_by_severity[sev] = findings_by_severity.get(sev, 0) + 1
            flat_findings.append(
                (
                    severity_rank(f.severity),
                    {
                        "probe": r.probe,
                        "code": f.code,
                        "severity": sev,
                        "message": f.message,
                        "target": _target_label(r.target),
                    },
                )
            )

    # Highest-severity findings first, capped so the summary stays scannable.
    top = [f for _, f in sorted(flat_findings, key=lambda t: t[0], reverse=True) if _ >= severity_rank(Severity.WARNING)]

    return {
        "status": worst.value if isinstance(worst, Status) else str(worst),
        "targets_failed": targets_failed,
        "findings_by_severity": findings_by_severity,
        "top_findings": top[:10],
        "probes_run": probes_run,
        "probes_errored": probes_errored,
    }
