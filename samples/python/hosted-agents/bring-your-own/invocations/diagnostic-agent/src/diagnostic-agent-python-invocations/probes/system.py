# Copyright (c) Microsoft. All rights reserved.

"""System-context probes: container info and the environment dump."""

from __future__ import annotations

import os
import time
from typing import Any

from framework import probelib
from framework.context import ProbeContext
from framework.contract import ProbeResult, Severity, Status, finding, result
from framework.registry import register


@register
class ContainerInfoProbe:
    id = "container.info"
    version = 1
    order = 5

    def applies_to(self, ctx: ProbeContext) -> bool:
        return ctx.include_container

    def run(self, ctx: ProbeContext) -> list[ProbeResult]:
        t0 = time.perf_counter()
        info = probelib.probe_container_info()
        rc = info.get("resolv_conf") or {}
        ns = rc.get("nameservers") if isinstance(rc, dict) else None
        return [
            result(
                self.id,
                self.version,
                Status.OK,
                target={"kind": "container"},
                summary=f"host={info.get('hostname')} resolvers={info.get('resolvers')}",
                metrics={"resolver_count": len(info.get("resolvers") or [])},
                evidence={**info},
                elapsed_ms=round((time.perf_counter() - t0) * 1000, 1),
            )
        ]


@register
class EnvDumpProbe:
    id = "env.dump"
    version = 1
    order = 6

    def applies_to(self, ctx: ProbeContext) -> bool:
        return ctx.include_env

    def run(self, ctx: ProbeContext) -> list[ProbeResult]:
        t0 = time.perf_counter()
        dump = probelib.probe_env_dump(dict(os.environ))
        values = dump.get("values") or {}
        findings: list[Any] = []
        # A single-nameserver resolv.conf makes any DNS drop fatal — surface it.
        if len(probelib.resolvers()) == 1:
            findings.append(
                finding(
                    "SINGLE_RESOLVER",
                    Severity.INFO,
                    "Only one nameserver in /etc/resolv.conf; a single DNS packet drop becomes a hard failure.",
                    remediation="Consider a second resolver or a VNet-local Private Resolver inbound endpoint.",
                )
            )
        return [
            result(
                self.id,
                self.version,
                Status.OK,
                target={"kind": "container"},
                summary=f"{len(values)} allowlisted environment variables captured.",
                findings=findings,
                metrics={"env_var_count": len(values)},
                evidence={**dump},
                elapsed_ms=round((time.perf_counter() - t0) * 1000, 1),
            )
        ]
