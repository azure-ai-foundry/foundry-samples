# Copyright (c) Microsoft. All rights reserved.

"""Time-spaced OS DNS sampling for propagation-delay diagnosis."""

from __future__ import annotations

import time
from datetime import datetime, timezone
from typing import Any

from framework import probelib
from framework.context import ProbeContext
from framework.contract import ProbeResult, Severity, Status, finding, result, status_from_findings
from framework.registry import register


@register
class DnsPropagationProbe:
    id = "dns.propagation"
    version = 1
    order = 1

    _CACHE_KEY = "dns.propagation.window"

    def applies_to(self, ctx: ProbeContext) -> bool:
        return ctx.dns_propagation_probe and bool(ctx.hosts)

    def pre_snapshot(self, ctx: ProbeContext) -> None:
        started = time.monotonic()
        ctx.cache[self._CACHE_KEY] = {
            "started": started,
            "attempts": {host: [self._sample(host, started)] for host in ctx.hosts},
        }

    def run(self, ctx: ProbeContext) -> list[ProbeResult]:
        state = ctx.cache.pop(self._CACHE_KEY, None)
        if state is None:
            started = time.monotonic()
            attempts = {host: [self._sample(host, started)] for host in ctx.hosts}
        else:
            started = state["started"]
            attempts = state["attempts"]

        deadline = started + ctx.dns_propagation_duration_sec
        next_sample = started + ctx.dns_propagation_interval_sec

        while next_sample < deadline:
            now = time.monotonic()
            time.sleep(max(0.0, next_sample - now))
            for host in ctx.hosts:
                attempts[host].append(self._sample(host, started))
            next_sample += ctx.dns_propagation_interval_sec

        if deadline > started:
            time.sleep(max(0.0, deadline - time.monotonic()))
            for host in ctx.hosts:
                attempts[host].append(self._sample(host, started))

        elapsed_sec = time.monotonic() - started
        return [self._build_result(host, ctx, attempts[host], elapsed_sec) for host in ctx.hosts]

    @staticmethod
    def _sample(host: str, started: float) -> dict[str, Any]:
        elapsed = time.monotonic() - started
        try:
            dns = probelib.probe_dns(host)
        except Exception as exc:  # noqa: BLE001 — one sample must not abort the observation window
            dns = {"status": "FAIL", "err": type(exc).__name__, "msg": str(exc)}

        attempt = {
            "elapsed_sec": round(elapsed, 3),
            "timestamp_utc": datetime.now(timezone.utc).isoformat(),
            "status": "ok" if dns.get("status") == "ok" else "fail",
            "ips": dns.get("ips") or [],
        }
        if attempt["status"] == "fail":
            attempt["error"] = dns.get("err") or "FAIL"
            attempt["message"] = dns.get("msg", "")
        return attempt

    def _build_result(
        self,
        host: str,
        ctx: ProbeContext,
        attempts: list[dict[str, Any]],
        elapsed_sec: float,
    ) -> ProbeResult:
        successes = [a for a in attempts if a["status"] == "ok"]
        failures = [a for a in attempts if a["status"] == "fail"]
        first_success = successes[0]["elapsed_sec"] if successes else None
        last_failure = failures[-1]["elapsed_sec"] if failures else None
        initial_failure = attempts[0]["status"] == "fail"
        recovered = initial_failure and first_success is not None
        persisted_past_threshold = initial_failure and (
            first_success is None or first_success > ctx.dns_propagation_threshold_sec
        )
        failures_after_success = bool(
            first_success is not None
            and any(a["status"] == "fail" and a["elapsed_sec"] > first_success for a in attempts)
        )

        findings = []
        if not successes:
            findings.append(
                finding(
                    "DNS_FAILURE_PERSISTED",
                    Severity.ERROR,
                    f"DNS failed for the full {elapsed_sec:.1f}-second observation window.",
                    remediation="Check managed-network DNS configuration and retry after network-connection propagation.",
                )
            )
        elif recovered and persisted_past_threshold:
            findings.append(
                finding(
                    "DNS_PROPAGATION_DELAY",
                    Severity.WARNING,
                    f"DNS first succeeded after {first_success:.1f} seconds, beyond the "
                    f"{ctx.dns_propagation_threshold_sec:.1f}-second threshold.",
                    remediation="Allow for network-connection propagation before starting dependent workloads.",
                )
            )
        elif recovered:
            findings.append(
                finding(
                    "DNS_INITIAL_INSTABILITY",
                    Severity.WARNING,
                    f"DNS failed at invocation start and recovered after {first_success:.1f} seconds.",
                    remediation="Delay dependent network calls until DNS is stable, and investigate startup-time propagation.",
                )
            )
        if failures_after_success:
            findings.append(
                finding(
                    "DNS_INTERMITTENT_OVER_TIME",
                    Severity.WARNING,
                    "DNS failed again after a successful lookup during the observation window.",
                    remediation="Investigate resolver or DNS-path intermittency rather than propagation delay alone.",
                )
            )

        metrics: dict[str, Any] = {
            "duration_sec": round(elapsed_sec, 3),
            "interval_sec": ctx.dns_propagation_interval_sec,
            "threshold_sec": ctx.dns_propagation_threshold_sec,
            "attempt_count": len(attempts),
            "failure_count": len(failures),
            "failure_rate": round(len(failures) / len(attempts), 3),
            "persisted_past_threshold": 1 if persisted_past_threshold else 0,
        }
        if first_success is not None:
            metrics["first_success_after_sec"] = first_success
        if last_failure is not None:
            metrics["last_failure_after_sec"] = last_failure

        if not successes:
            summary = f"DNS failed throughout {elapsed_sec:.1f}s observation window"
        elif recovered:
            summary = f"DNS recovered after {first_success:.1f}s"
        elif failures:
            summary = "DNS was intermittent during the observation window"
        else:
            summary = f"DNS remained healthy for {elapsed_sec:.1f}s"

        return result(
            self.id,
            self.version,
            status_from_findings(findings, default=Status.OK),
            target={"host": host},
            summary=summary,
            findings=findings,
            metrics=metrics,
            evidence={"attempts": attempts},
            elapsed_ms=round(elapsed_sec * 1000, 1),
        )
