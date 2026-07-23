# Copyright (c) Microsoft. All rights reserved.

"""``conn.direct`` — raw ip:port / host:port reachability WITHOUT DNS.

Isolates a pure network-path problem (NSG/UDR/PE-disconnected) from a DNS
problem: if a name fails to resolve but its known private IP is reachable here,
the break is DNS, not the network.
"""

from __future__ import annotations

import time

from framework import probelib
from framework.context import ProbeContext
from framework.contract import ProbeResult, Severity, Status, finding, result
from framework.registry import register


@register
class DirectProbe:
    id = "conn.direct"
    version = 1
    order = 30

    def applies_to(self, ctx: ProbeContext) -> bool:
        return bool(ctx.direct_targets)

    def run(self, ctx: ProbeContext) -> list[ProbeResult]:
        out: list[ProbeResult] = []
        for target in ctx.direct_targets:
            t0 = time.perf_counter()
            raw = probelib.probe_direct(target, ctx.tcp_timeout_sec)
            tcp = raw.get("tcp") or {}
            ok = tcp.get("status") == "ok"
            findings = []
            if not ok:
                findings.append(
                    finding(
                        "TCP_UNREACHABLE",
                        Severity.ERROR,
                        f"TCP to {target} failed ({tcp.get('err')}). {tcp.get('hint', '')}",
                        remediation="Check NSG/UDR/peering and private-endpoint connection state.",
                    )
                )
            out.append(
                result(
                    self.id,
                    self.version,
                    Status.OK if ok else Status.FAIL,
                    target={"target": target},
                    summary=f"TCP {'ok' if ok else 'FAILED'} to {target}",
                    findings=findings,
                    metrics={"tcp_ms": tcp.get("ms")} if tcp.get("ms") is not None else {},
                    evidence={**raw},
                    elapsed_ms=round((time.perf_counter() - t0) * 1000, 1),
                )
            )
        return out
