# Copyright (c) Microsoft. All rights reserved.

"""The stable, probe-agnostic result contract for the diagnostic agent.

Every probe — no matter who contributes it — emits the **same** ``ProbeResult``
envelope, so consumers (dashboards, an LLM reading the JSON, an aggregator) can
summarize a probe they have never seen without special-casing it. This module is
the single source of truth for that shape and is intentionally dependency-free
(stdlib only) — the network is the thing being diagnosed, so nothing here may
trigger an import-time package fetch.

Envelope (per probe)::

    {
      "probe":         "dns.parallel",   # namespaced id — group related probes
      "probe_version": 2,                # evolves independently per probe
      "status":        "ok",             # ok | warn | fail | error | skipped
      "target":        {"host": "..."},  # what this result is about
      "summary":       "one line",
      "findings":      [ {code, severity, message, remediation} ],
      "metrics":       {"both_ok_rate": 1.0},   # flat numerics for dashboards
      "evidence":      { ... },                 # verbose raw detail (gated)
      "elapsed_ms":    12.3
    }

The four-way split matters:

* ``status``   — the probe's own verdict, from a fixed enum. ``error`` (the probe
                 itself crashed) is distinct from ``fail`` (it found a real
                 problem), so a generic aggregator can compute a worst-status
                 rollup and a bad probe never masquerades as an incident.
* ``findings`` — discrete, coded issues with severity + remediation; the
                 machine-readable "what's wrong and what to do".
* ``metrics``  — a flat numeric map for time-series / dashboards, no parsing.
* ``evidence`` — the verbose raw detail (dig text, CNAME chains, per-resolver
                 records), gated behind ``include_evidence`` so payloads stay lean.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Any

# Bump when the envelope shape changes in a backward-incompatible way. Additive
# changes (new optional keys) do NOT require a bump.
SCHEMA_VERSION = 1


class Status(str, Enum):
    """A probe's own verdict. ``error`` means the probe itself failed to run;
    ``fail`` means the probe ran and found a real problem."""

    OK = "ok"
    WARN = "warn"
    FAIL = "fail"
    ERROR = "error"
    SKIPPED = "skipped"


class Severity(str, Enum):
    INFO = "info"
    WARNING = "warning"
    ERROR = "error"
    CRITICAL = "critical"


# Rank tables let the aggregator roll up heterogeneous results without knowing
# what any probe does. Higher = worse.
_STATUS_RANK: dict[Status, int] = {
    Status.SKIPPED: 0,
    Status.OK: 0,
    Status.WARN: 1,
    Status.FAIL: 2,
    Status.ERROR: 3,
}
_SEVERITY_RANK: dict[Severity, int] = {
    Severity.INFO: 0,
    Severity.WARNING: 1,
    Severity.ERROR: 2,
    Severity.CRITICAL: 3,
}


def _coerce_status(value: Status | str) -> Status:
    return value if isinstance(value, Status) else Status(str(value))


def _coerce_severity(value: Severity | str) -> Severity:
    return value if isinstance(value, Severity) else Severity(str(value))


@dataclass
class Finding:
    """A discrete, coded issue discovered by a probe."""

    code: str
    severity: Severity = Severity.INFO
    message: str = ""
    remediation: str | None = None

    def to_dict(self) -> dict[str, Any]:
        return {
            "code": self.code,
            "severity": _coerce_severity(self.severity).value,
            "message": self.message,
            "remediation": self.remediation,
        }


@dataclass
class ProbeResult:
    """The canonical, uniform result every probe emits."""

    probe: str
    probe_version: int
    status: Status
    target: dict[str, Any] = field(default_factory=dict)
    summary: str = ""
    findings: list[Finding] = field(default_factory=list)
    metrics: dict[str, Any] = field(default_factory=dict)
    evidence: dict[str, Any] = field(default_factory=dict)
    elapsed_ms: float = 0.0

    def to_dict(self, include_evidence: bool = True) -> dict[str, Any]:
        out: dict[str, Any] = {
            "probe": self.probe,
            "probe_version": self.probe_version,
            "status": _coerce_status(self.status).value,
            "target": self.target,
            "summary": self.summary,
            "findings": [f.to_dict() for f in self.findings],
            "metrics": self.metrics,
            "elapsed_ms": self.elapsed_ms,
        }
        # Evidence is the verbose raw detail; gate it so default payloads stay
        # small. The legacy adapter always needs it, so it reads .evidence direct.
        out["evidence"] = self.evidence if include_evidence else {}
        return out


# ── construction helpers ─────────────────────────────────────────────────────
# Probes use these so they cannot accidentally emit a malformed envelope.


def finding(
    code: str,
    severity: Severity | str = Severity.INFO,
    message: str = "",
    remediation: str | None = None,
) -> Finding:
    return Finding(code=code, severity=_coerce_severity(severity), message=message, remediation=remediation)


def status_from_findings(findings: list[Finding], default: Status = Status.OK) -> Status:
    """Derive a probe ``status`` from its findings' worst severity:
    warning -> warn, error/critical -> fail. No findings -> ``default``."""
    worst = default
    for f in findings:
        rank = _SEVERITY_RANK.get(_coerce_severity(f.severity), 0)
        if rank >= _SEVERITY_RANK[Severity.ERROR]:
            return Status.FAIL
        if rank == _SEVERITY_RANK[Severity.WARNING] and _STATUS_RANK[worst] < _STATUS_RANK[Status.WARN]:
            worst = Status.WARN
    return worst


def result(
    probe: str,
    probe_version: int,
    status: Status | str,
    target: dict[str, Any] | None = None,
    summary: str = "",
    findings: list[Finding] | None = None,
    metrics: dict[str, Any] | None = None,
    evidence: dict[str, Any] | None = None,
    elapsed_ms: float = 0.0,
) -> ProbeResult:
    return ProbeResult(
        probe=probe,
        probe_version=probe_version,
        status=_coerce_status(status),
        target=target or {},
        summary=summary,
        findings=findings or [],
        metrics=metrics or {},
        evidence=evidence or {},
        elapsed_ms=elapsed_ms,
    )


def skipped_result(probe: str, probe_version: int, target: dict[str, Any] | None, reason: str) -> ProbeResult:
    return result(probe, probe_version, Status.SKIPPED, target=target, summary=reason, evidence={"reason": reason})


def error_result(
    probe: str,
    probe_version: int,
    target: dict[str, Any] | None,
    exc: BaseException,
    traceback_str: str | None = None,
) -> ProbeResult:
    """Synthesized by the runner when a probe raises — its failure is contained
    and clearly marked ``error`` (not ``fail``), so a buggy probe can never abort
    sibling probes or masquerade as a real incident."""
    ev: dict[str, Any] = {"err": type(exc).__name__, "msg": str(exc)[:300]}
    if traceback_str:
        ev["traceback"] = traceback_str
    return result(
        probe,
        probe_version,
        Status.ERROR,
        target=target,
        summary=f"Probe '{probe}' crashed and was isolated by the runner.",
        findings=[
            finding(
                "PROBE_ERROR",
                Severity.ERROR,
                f"Unhandled {type(exc).__name__} in probe '{probe}': {str(exc)[:200]}",
                remediation=f"Check the probe that owns the '{probe.split('.')[0]}.*' namespace.",
            )
        ],
        evidence=ev,
    )


def worst_status(results: list[ProbeResult]) -> Status:
    worst = Status.OK
    for r in results:
        st = _coerce_status(r.status)
        if _STATUS_RANK[st] > _STATUS_RANK[worst]:
            worst = st
    return worst


def status_rank(status: Status | str) -> int:
    return _STATUS_RANK[_coerce_status(status)]


def severity_rank(severity: Severity | str) -> int:
    return _SEVERITY_RANK[_coerce_severity(severity)]
