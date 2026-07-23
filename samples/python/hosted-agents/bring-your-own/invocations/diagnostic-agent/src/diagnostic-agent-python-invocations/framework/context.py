# Copyright (c) Microsoft. All rights reserved.

"""Per-invocation configuration and shared scratch space handed to every probe.

``ProbeContext`` is built once from the request body and passed (read-only from
the probe's point of view) to each probe's ``applies_to`` / ``run``. Probes read
their configuration from here instead of re-parsing the request or reading global
state — this is the dependency-injection seam (DIP) that keeps probes decoupled
from the handler and from each other.

``cache`` is a small per-invocation dict for cross-probe hand-off (e.g. the DNS
probe stashes resolved IPs there for the connectivity probe, and a probe's
``pre_snapshot`` stashes a baseline for a later delta). It is the ONLY shared
mutable state and is scoped to a single invocation.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

try:
    from framework import net_probe
except Exception:  # noqa: BLE001 — never let a probe-module import kill the agent
    net_probe = None  # type: ignore[assignment]

_DEFAULT_PUBLIC_HOSTS = [
    "https://www.microsoft.com/",
    "https://management.azure.com/metadata/endpoints?api-version=2020-09-01",
    "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration",
]


@dataclass
class ProbeContext:
    spec: dict[str, Any]

    hosts: list[str]
    public_hosts: list[str]
    direct_targets: list[str]

    # DNS options
    sys_resolvers: list[str]
    resolvers_extra: list[str]
    all_resolvers: list[str]
    record_types: list[str]
    raw_dns: bool
    dns_attempts: int
    gai_attempts: int
    parallel_probe: bool

    # timeouts
    tcp_timeout_sec: int
    http_timeout_sec: int
    dns_timeout_sec: int

    # section toggles
    include_env: bool
    include_container: bool

    # response shaping
    include_evidence: bool

    cache: dict[str, Any] = field(default_factory=dict)

    @classmethod
    def from_spec(cls, spec: dict[str, Any]) -> "ProbeContext":
        spec = spec or {}
        sys_resolvers = (
            net_probe.parse_resolv_conf().get("nameservers", []) if net_probe is not None else []
        )
        resolvers_extra = spec.get("resolvers") or []
        all_resolvers = list(dict.fromkeys(list(sys_resolvers) + list(resolvers_extra)))

        public_hosts = spec.get("public_hosts")
        if public_hosts is None:
            public_hosts = list(_DEFAULT_PUBLIC_HOSTS)

        return cls(
            spec=spec,
            hosts=spec.get("hosts") or [],
            public_hosts=public_hosts,
            direct_targets=spec.get("direct_targets") or [],
            sys_resolvers=sys_resolvers,
            resolvers_extra=resolvers_extra,
            all_resolvers=all_resolvers,
            record_types=spec.get("record_types") or ["A", "AAAA"],
            raw_dns=bool(spec.get("raw_dns", True)),
            dns_attempts=int(spec.get("dns_attempts") or 1),
            gai_attempts=int(spec.get("gai_attempts") or 1),
            parallel_probe=bool(spec.get("parallel_probe", False)),
            tcp_timeout_sec=int(spec.get("tcp_timeout_sec") or 5),
            http_timeout_sec=int(spec.get("http_timeout_sec") or 10),
            dns_timeout_sec=int(spec.get("dns_timeout_sec") or 5),
            include_env=bool(spec.get("include_env_dump", True)),
            include_container=bool(spec.get("include_container_info", True)),
            include_evidence=bool(spec.get("include_evidence", True)),
        )
