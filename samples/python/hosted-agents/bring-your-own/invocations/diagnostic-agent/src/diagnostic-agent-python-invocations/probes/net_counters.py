# Copyright (c) Microsoft. All rights reserved.

"""``net.udp_counters`` — local UDP/interface drop counters, measured as a DELTA
bracketing the whole diagnostic pass.

This is a NEGATIVE CONTROL, not a localizer. When a DNS lookup intermittently
fails (``EAI_AGAIN``) while raw queries succeed, this probe answers one question:
"were any packets dropped at *this* sandbox's NIC / UDP socket during the run?"

* All deltas ~0  -> the loss is NOT local (it is on the network path or at the
  upstream DNS server); reinforces a path/server root cause.
* Non-zero UDP ``InErrors`` / ``RcvbufErrors`` / socket drops -> a local
  contribution worth chasing (socket buffer exhaustion, CPU starvation).

Note: interface ``rx_errors`` / ``tx_errors`` are near-useless on virtual NICs
(virtio has no CRC/framing errors) and are reported only for completeness. The
``/proc/net/snmp`` UDP counters and ``/proc/net/udp`` socket drops are the useful
signals.

Uses the runner's ``pre_snapshot`` hook to capture a baseline before any probe
runs, then reports the delta in ``run`` (which the runner schedules last).
"""

from __future__ import annotations

import os
import time
from typing import Any

from framework import probelib
from framework.context import ProbeContext
from framework.contract import ProbeResult, Severity, Status, finding, result
from framework.registry import register

_CACHE_KEY = "net_counters_before"


def _read_snmp_udp() -> dict[str, int]:
    text = probelib.read_text("/proc/net/snmp", max_bytes=16384) or ""
    header: list[str] | None = None
    values: list[str] | None = None
    for line in text.splitlines():
        if not line.startswith("Udp:"):
            continue
        parts = line.split()[1:]
        if parts and parts[0].lstrip("-").isdigit():
            values = parts
        else:
            header = parts
    out: dict[str, int] = {}
    if header and values and len(header) == len(values):
        for k, v in zip(header, values):
            try:
                out[k] = int(v)
            except ValueError:
                pass
    return out


def _read_udp_socket_drops(path: str) -> int:
    text = probelib.read_text(path, max_bytes=131072) or ""
    total = 0
    for line in text.splitlines()[1:]:
        cols = line.split()
        if len(cols) >= 13:
            try:
                total += int(cols[-1])  # last column = drops
            except ValueError:
                pass
    return total


def _read_iface_stats() -> dict[str, int]:
    out: dict[str, int] = {}
    try:
        ifaces = os.listdir("/sys/class/net")
    except OSError:
        return out
    for iface in ifaces:
        for stat in ("rx_dropped", "tx_dropped", "rx_errors", "tx_errors"):
            v = probelib.read_text(f"/sys/class/net/{iface}/statistics/{stat}")
            if v is None:
                continue
            try:
                out[f"{iface}.{stat}"] = int(v.strip())
            except ValueError:
                pass
    return out


def read_net_counters() -> dict[str, Any]:
    snmp = _read_snmp_udp()
    iface = _read_iface_stats()
    return {
        "udp_in_errors": snmp.get("InErrors"),
        "udp_rcvbuf_errors": snmp.get("RcvbufErrors"),
        "udp_sndbuf_errors": snmp.get("SndbufErrors"),
        "udp_in_datagrams": snmp.get("InDatagrams"),
        "udp_socket_drops": _read_udp_socket_drops("/proc/net/udp") + _read_udp_socket_drops("/proc/net/udp6"),
        "iface": iface,
    }


def _delta(after: Any, before: Any) -> Any:
    if isinstance(after, (int, float)) and isinstance(before, (int, float)):
        return after - before
    return None


@register
class NetCountersProbe:
    id = "net.udp_counters"
    version = 1
    order = 90  # run last, so the delta brackets the DNS/connectivity work

    def applies_to(self, ctx: ProbeContext) -> bool:
        # Only meaningful when there is DNS/network activity to bracket.
        return bool(ctx.hosts)

    def pre_snapshot(self, ctx: ProbeContext) -> None:
        ctx.cache[_CACHE_KEY] = read_net_counters()

    def run(self, ctx: ProbeContext) -> list[ProbeResult]:
        t0 = time.perf_counter()
        before = ctx.cache.get(_CACHE_KEY) or {}
        after = read_net_counters()

        scalar_keys = ("udp_in_errors", "udp_rcvbuf_errors", "udp_sndbuf_errors", "udp_socket_drops")
        metrics: dict[str, Any] = {}
        for k in scalar_keys:
            metrics[f"{k}_delta"] = _delta(after.get(k), before.get(k))

        # Interface rx/tx dropped/errors deltas, summed across interfaces.
        iface_before = before.get("iface") or {}
        iface_after = after.get("iface") or {}
        for stat in ("rx_dropped", "tx_dropped", "rx_errors", "tx_errors"):
            tot = 0
            seen = False
            for key, val in iface_after.items():
                if key.endswith("." + stat):
                    d = _delta(val, iface_before.get(key))
                    if d is not None:
                        tot += d
                        seen = True
            metrics[f"iface_{stat}_delta"] = tot if seen else None

        # A drop is anything non-zero among the meaningful UDP/socket counters.
        meaningful = [
            metrics.get("udp_in_errors_delta"),
            metrics.get("udp_rcvbuf_errors_delta"),
            metrics.get("udp_socket_drops_delta"),
            metrics.get("iface_rx_dropped_delta"),
        ]
        had_drops = any(isinstance(v, (int, float)) and v > 0 for v in meaningful)

        if had_drops:
            findings = [
                finding(
                    "LOCAL_UDP_DROPS",
                    Severity.WARNING,
                    "Local UDP/interface drops occurred during the run — part of the loss may be at this "
                    "sandbox (socket buffer exhaustion or CPU starvation), not only on the network path.",
                    remediation="Inspect the per-counter deltas; correlate with concurrent load.",
                )
            ]
            status = Status.WARN
            summary = "Local UDP/interface drops detected during the diagnostic window."
        else:
            findings = [
                finding(
                    "LOCAL_UDP_CLEAN",
                    Severity.INFO,
                    "No local UDP/interface drops during the run — DNS loss is not at this sandbox's NIC/socket.",
                )
            ]
            status = Status.OK
            summary = "No local UDP/interface drops; loss (if any) is on the path or upstream."

        return [
            result(
                self.id,
                self.version,
                status,
                target={"kind": "container"},
                summary=summary,
                findings=findings,
                metrics=metrics,
                evidence={
                    "before": before,
                    "after": after,
                    "sources": ["/proc/net/snmp", "/proc/net/udp", "/proc/net/udp6", "/sys/class/net/*/statistics"],
                    "note": "Interface rx/tx_errors are near-useless on virtio; UDP counters are the signal.",
                },
                elapsed_ms=round((time.perf_counter() - t0) * 1000, 1),
            )
        ]
