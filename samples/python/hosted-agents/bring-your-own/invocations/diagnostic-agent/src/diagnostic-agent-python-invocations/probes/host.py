# Copyright (c) Microsoft. All rights reserved.

"""``dns.*`` + ``conn.*`` — the composite host reachability flow.

For each requested host this probe runs the full chain the diagnostic agent is
built around and emits one uniform ``ProbeResult`` per facet:

* ``dns.getaddrinfo`` — the OS resolver (what the app/SDK actually sees), plus
  the repeated-getaddrinfo failure rate and the getaddrinfo-vs-raw comparison.
* ``dns.raw``         — the per-resolver raw DNS truth (rcode, CNAME chain,
  timeout rate, UDP-vs-TCP), keyed on the CONFIGURED resolver.
* ``dns.parallel``    — glibc-style parallel A+AAAA on one socket (opt-in).
* ``conn.tcp`` / ``conn.tls`` / ``conn.http`` — layered reachability, using the
  resolved IP (falling back to the raw-resolved IP when getaddrinfo fails).

The DNS/connect chain is kept in one probe because the facets share the resolved
IP and short-circuit as a unit — that is a single, coherent responsibility ("can
the runtime reach this host, and if not, where does it break?"). Independent
capabilities (e.g. ``net.udp_counters``) live in their own probe modules.
"""

from __future__ import annotations

import logging
import time
import traceback
from typing import Any

from framework import probelib
from framework.context import ProbeContext
from framework.contract import ProbeResult, Severity, Status, error_result, finding, result, status_from_findings
from framework.registry import register

logger = logging.getLogger("diagnostic_agent.host")

try:
    from framework import net_probe
except Exception:  # noqa: BLE001
    net_probe = None  # type: ignore[assignment]

# Raw-DNS classification -> finding severity.
_DNS_SEVERITY: dict[str, Severity] = {
    "DNS_OK_PRIVATE": Severity.INFO,
    "DNS_OK_PUBLIC": Severity.INFO,
    "DNS_OK_PRIVATE_INTERMITTENT": Severity.WARNING,
    "DNS_INTERMITTENT": Severity.WARNING,
    "DNS_UDP_DROP_TCP_OK": Severity.WARNING,
    "DNS_OK_PUBLIC_FOR_PRIVATE": Severity.WARNING,
    "DNS_TIMEOUT": Severity.ERROR,
    "DNS_SERVFAIL": Severity.ERROR,
    "DNS_REFUSED": Severity.ERROR,
    "DNS_NXDOMAIN": Severity.ERROR,
    "DNS_NODATA": Severity.ERROR,
    "DNS_UNKNOWN": Severity.ERROR,
    "PROBE_ERROR": Severity.ERROR,
}


def _sev(classification: str) -> Severity:
    return _DNS_SEVERITY.get(classification, Severity.WARNING)


def _configured_resolver_record(dig: dict[str, Any]) -> dict[str, Any] | None:
    """Return the A-record aggregate for the first CONFIGURED resolver."""
    for r in dig.get("per_resolver", []) or []:
        if r.get("configured"):
            return (r.get("records") or {}).get("A") or {}
    return None


@register
class HostReachabilityProbe:
    id = "host.reachability"
    version = 1
    order = 20

    def applies_to(self, ctx: ProbeContext) -> bool:
        return bool(ctx.hosts)

    def run(self, ctx: ProbeContext) -> list[ProbeResult]:
        out: list[ProbeResult] = []
        for host in ctx.hosts:
            try:
                out.extend(self._run_host(host, ctx))
            except Exception as e:  # noqa: BLE001 — one host must never abort the others
                logger.error("host probe crashed host=%s err=%s", host, type(e).__name__)
                out.append(error_result(self.id, self.version, {"host": host}, e, traceback_str=traceback.format_exc()))
        return out

    # ── per-host chain ───────────────────────────────────────────────────────
    def _run_host(self, host: str, ctx: ProbeContext) -> list[ProbeResult]:
        results: list[ProbeResult] = []
        target = {"host": host}

        # 1) OS resolver (getaddrinfo). probe_dns never raises.
        dns = probelib.probe_dns(host)
        gai_ok = dns.get("status") == "ok"
        gai_ips = dns.get("ips") if gai_ok else None
        gai_err = None if gai_ok else (dns.get("err") or "FAIL")
        ips_for_connect: list[str] = list(gai_ips or [])

        # 2) Raw per-resolver DNS + getaddrinfo-vs-raw (isolated sub-step).
        dig: dict[str, Any] = {}
        vs_raw: dict[str, Any] = {}
        raw_error: Exception | None = None
        if ctx.raw_dns and net_probe is not None:
            rs = ctx.all_resolvers or (net_probe.parse_resolv_conf().get("nameservers", []))
            if rs:
                try:
                    dig = net_probe.dig_host(
                        host,
                        rs,
                        record_types=ctx.record_types,
                        timeout=ctx.dns_timeout_sec,
                        attempts=ctx.dns_attempts,
                        configured_resolvers=ctx.sys_resolvers or rs,
                    )
                    vs_raw = net_probe.getaddrinfo_vs_raw(host, gai_ips, gai_err, dig)
                    if not ips_for_connect:
                        ips_for_connect = vs_raw.get("raw_ips", []) or []
                except Exception as e:  # noqa: BLE001 — raw DNS must not abort this host's other checks
                    raw_error = e
                    logger.exception("raw DNS failed host=%s", host)

        # 2b) Repeated getaddrinfo (failure rate) — isolated sub-step.
        repeat: dict[str, Any] = {}
        if ctx.gai_attempts and ctx.gai_attempts > 1:
            try:
                repeat = {"attempts": ctx.gai_attempts, "successes": 0, "failures": 0, "errs": {}, "ips": None}
                for _ in range(ctx.gai_attempts):
                    dd = probelib.probe_dns(host)
                    if dd.get("status") == "ok":
                        repeat["successes"] += 1
                        if repeat["ips"] is None:
                            repeat["ips"] = dd.get("ips")
                    else:
                        repeat["failures"] += 1
                        e = dd.get("err", "?")
                        repeat["errs"][e] = repeat["errs"].get(e, 0) + 1
                repeat["failure_rate"] = round(repeat["failures"] / ctx.gai_attempts, 2)
            except Exception:  # noqa: BLE001 — a repeat-probe crash must not abort the host
                logger.exception("gai repeat failed host=%s", host)
                repeat = {}

        results.append(self._dns_getaddrinfo_result(host, target, dns, gai_ok, gai_err, repeat, vs_raw))

        # 2c) Raw DNS result (or an isolated error for just this sub-step).
        if dig:
            results.append(self._dns_raw_result(host, target, dig))
        elif raw_error is not None:
            results.append(error_result("dns.raw", self.version, target, raw_error))

        # 2d) Parallel A+AAAA (opt-in) — isolated sub-step.
        if ctx.parallel_probe and net_probe is not None:
            try:
                rs2 = ctx.sys_resolvers or ctx.all_resolvers or net_probe.parse_resolv_conf().get("nameservers", [])
                parallel = [
                    net_probe.parallel_dual_stats(r, host, timeout=ctx.dns_timeout_sec, attempts=max(ctx.dns_attempts, 10))
                    for r in rs2
                ]
                results.append(self._dns_parallel_result(host, target, parallel))
            except Exception as e:  # noqa: BLE001 — parallel probe crash must not abort the host
                results.append(error_result("dns.parallel", self.version, target, e))

        # 3) Connectivity chain (shares the resolved IP, short-circuits as a unit) — isolated.
        try:
            results.extend(self._connectivity(host, target, ips_for_connect, ctx))
        except Exception as e:  # noqa: BLE001 — connectivity crash must not abort sibling results
            results.append(error_result("conn.tcp", self.version, target, e))
        return results

    # ── result builders ──────────────────────────────────────────────────────
    def _dns_getaddrinfo_result(self, host, target, dns, gai_ok, gai_err, repeat, vs_raw) -> ProbeResult:
        findings = []
        if not gai_ok:
            findings.append(
                finding(
                    "GETADDRINFO_FAIL",
                    Severity.ERROR,
                    f"getaddrinfo failed ({gai_err}): {dns.get('msg', '')}",
                    remediation=dns.get("hint"),
                )
            )
        vr_cls = vs_raw.get("classification")
        if vr_cls == "GAI_FAIL_RAW_OK":
            findings.append(finding("GAI_FAIL_RAW_OK", Severity.ERROR, vs_raw.get("hint", ""),
                                    remediation="Root cause is the DNS path/server, not the app; see dns.raw."))
        elif vr_cls == "GAI_RAW_MISMATCH":
            findings.append(finding("GAI_RAW_MISMATCH", Severity.WARNING, vs_raw.get("hint", "")))
        if repeat and repeat.get("failure_rate", 0) > 0:
            findings.append(
                finding(
                    "GAI_INTERMITTENT",
                    Severity.WARNING,
                    f"getaddrinfo failed {repeat['failures']}/{repeat['attempts']} times "
                    f"(rate {repeat['failure_rate']}).",
                )
            )

        metrics: dict[str, Any] = {}
        if repeat:
            metrics.update(
                {
                    "gai_attempts": repeat.get("attempts"),
                    "gai_failures": repeat.get("failures"),
                    "gai_failure_rate": repeat.get("failure_rate"),
                }
            )
        else:
            metrics["gai_ok"] = 1 if gai_ok else 0

        status = status_from_findings(findings, default=Status.OK if gai_ok else Status.FAIL)
        return result(
            "dns.getaddrinfo",
            self.version,
            status,
            target=target,
            summary=("getaddrinfo ok" if gai_ok else f"getaddrinfo FAILED ({gai_err})"),
            findings=findings,
            metrics=metrics,
            evidence={
                "getaddrinfo": dns,
                "repeat": repeat or None,
                "vs_raw": vs_raw or None,
            },
        )

    def _dns_raw_result(self, host, target, dig) -> ProbeResult:
        verdict = dig.get("verdict") or {}
        classes = verdict.get("classifications") or dig.get("classifications") or []
        findings = []
        for cls in classes:
            hint = ""
            for r in dig.get("per_resolver", []) or []:
                if r.get("classification") == cls:
                    hint = r.get("hint", "")
                    break
            sev = _sev(cls)
            if sev != Severity.INFO:
                findings.append(finding(cls, sev, hint))
        disagree = dig.get("resolver_disagreement")
        if disagree:
            findings.append(finding("RESOLVER_DISAGREE", Severity.INFO, disagree.get("hint", "")))

        metrics: dict[str, Any] = {}
        a_rec = _configured_resolver_record(dig)
        if a_rec:
            metrics.update(
                {
                    "a_udp_timeout_rate": a_rec.get("timeout_rate"),
                    "a_udp_avg_ms": a_rec.get("avg_ms"),
                    "a_udp_max_ms": a_rec.get("max_ms"),
                    "a_tcp_ok": 1 if (a_rec.get("tcp") or {}).get("answers") else 0,
                }
            )

        status = status_from_findings(findings, default=Status.OK)
        return result(
            "dns.raw",
            self.version,
            status,
            target=target,
            summary="; ".join(classes) if classes else "raw DNS probed",
            findings=findings,
            metrics=metrics,
            evidence={**dig},
        )

    def _dns_parallel_result(self, host, target, parallel) -> ProbeResult:
        findings = []
        metrics: dict[str, Any] = {}
        for stat in parallel:
            cls = stat.get("classification")
            if cls == "PARALLEL_DUAL_LOSS":
                findings.append(finding("PARALLEL_DUAL_LOSS", Severity.WARNING, stat.get("hint", "")))
            elif cls == "PARALLEL_DUAL_OK":
                findings.append(finding("PARALLEL_DUAL_OK", Severity.INFO, stat.get("hint", "")))
            if stat.get("configured", True) or "both_ok_rate" not in metrics:
                metrics = {
                    "both_ok_rate": stat.get("both_ok_rate"),
                    "a_lost": stat.get("a_lost"),
                    "aaaa_lost": stat.get("aaaa_lost"),
                    "avg_ms": stat.get("avg_ms"),
                }
        status = status_from_findings(findings, default=Status.OK)
        return result(
            "dns.parallel",
            self.version,
            status,
            target=target,
            summary=", ".join(s.get("classification", "") for s in parallel) or "parallel A+AAAA probed",
            findings=findings,
            metrics=metrics,
            evidence={"per_resolver": parallel},
        )

    def _connectivity(self, host, target, ips_for_connect, ctx) -> list[ProbeResult]:
        out: list[ProbeResult] = []
        if not ips_for_connect:
            out.append(
                result(
                    "conn.tcp", self.version, Status.SKIPPED, target=target,
                    summary="Skipped: name did not resolve to any IP (getaddrinfo + raw both empty).",
                    evidence={"reason": "no_resolved_ip"},
                )
            )
            return out
        ip = ips_for_connect[0]

        tcp = probelib.probe_tcp(ip, 443, ctx.tcp_timeout_sec)
        tcp_ok = tcp.get("status") == "ok"
        out.append(
            result(
                "conn.tcp", self.version, Status.OK if tcp_ok else Status.FAIL, target={**target, "ip": ip, "port": 443},
                summary=f"TCP 443 {'ok' if tcp_ok else 'FAILED'} to {ip}",
                findings=[] if tcp_ok else [finding("TCP_FAIL", Severity.ERROR, tcp.get("hint", ""), remediation="Check NSG/UDR/peering/PE state.")],
                metrics={"ms": tcp.get("ms")} if tcp.get("ms") is not None else {},
                evidence={**tcp},
            )
        )
        if not tcp_ok:
            return out

        tls = probelib.probe_tls(host, ip, 443, ctx.tcp_timeout_sec)
        tls_ok = tls.get("status") == "ok"
        out.append(
            result(
                "conn.tls", self.version, Status.OK if tls_ok else Status.FAIL, target={**target, "ip": ip},
                summary=f"TLS {'ok' if tls_ok else 'FAILED'} ({tls.get('version') or tls.get('err')})",
                findings=[] if tls_ok else [finding("TLS_FAIL", Severity.ERROR, tls.get("hint", ""))],
                metrics={"handshake_ms": tls.get("ms")} if tls.get("ms") is not None else {},
                evidence={**tls},
            )
        )
        if not tls_ok:
            return out

        # For ACR-shaped hosts (registry and data endpoints, both ending in
        # .azurecr.io), /v2/ is the canonical reachability test (returns 401 with
        # a useful WWW-Authenticate header). For everything else, hit /.
        path = "/v2/" if host.endswith(".azurecr.io") else "/"
        http = probelib.probe_http_get(f"https://{host}{path}", host_header=None, http_timeout_sec=ctx.http_timeout_sec)
        http_ok = http.get("status") == "ok"
        out.append(
            result(
                "conn.http", self.version, Status.OK if http_ok else Status.FAIL, target=target,
                summary=(f"HTTP {http.get('code')}" if http_ok else f"HTTP request FAILED ({http.get('err')})"),
                findings=[] if http_ok else [finding("HTTP_FAIL", Severity.ERROR, http.get("hint", ""))],
                metrics={"code": http.get("code"), "ms": http.get("ms")} if http_ok else {},
                evidence={**http},
            )
        )
        return out
