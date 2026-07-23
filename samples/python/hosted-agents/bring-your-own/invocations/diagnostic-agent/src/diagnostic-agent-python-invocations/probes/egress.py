# Copyright (c) Microsoft. All rights reserved.

"""``egress.public`` — plain HTTPS GET to a small fixed set of public Azure
endpoints, to confirm the sandbox has working outbound internet/egress."""

from __future__ import annotations

import time

from framework import probelib
from framework.context import ProbeContext
from framework.contract import ProbeResult, Severity, Status, finding, result
from framework.registry import register


@register
class EgressProbe:
    id = "egress.public"
    version = 1
    order = 40

    def applies_to(self, ctx: ProbeContext) -> bool:
        return bool(ctx.public_hosts)

    def run(self, ctx: ProbeContext) -> list[ProbeResult]:
        out: list[ProbeResult] = []
        for url in ctx.public_hosts:
            t0 = time.perf_counter()
            raw = probelib.probe_http_get(url, None, ctx.http_timeout_sec)
            ok = raw.get("status") == "ok"
            code = raw.get("code")
            findings = []
            if not ok:
                findings.append(
                    finding(
                        "EGRESS_FAIL",
                        Severity.WARNING,
                        f"HTTPS GET {url} failed ({raw.get('err')}). {raw.get('hint', '')}",
                    )
                )
            out.append(
                result(
                    self.id,
                    self.version,
                    Status.OK if ok else Status.WARN,
                    target={"url": url},
                    summary=f"{'HTTP ' + str(code) if code else 'FAILED'} {url}",
                    findings=findings,
                    metrics={"code": code, "ms": raw.get("ms")} if ok else {},
                    evidence={**raw},
                    elapsed_ms=round((time.perf_counter() - t0) * 1000, 1),
                )
            )
        return out
