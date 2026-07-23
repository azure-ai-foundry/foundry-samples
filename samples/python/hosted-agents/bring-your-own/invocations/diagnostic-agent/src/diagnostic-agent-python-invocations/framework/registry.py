# Copyright (c) Microsoft. All rights reserved.

"""Probe interface + registry.

A probe is any object satisfying :class:`Probe`. Anyone can add a probe by writing a
class and decorating it with :func:`register` — nothing else in the codebase
needs to change (Open/Closed). The runner discovers probes only through this
registry, so the handler never imports a concrete probe (Dependency Inversion).

    @register
    class MyProbe:
        id = "mymodule.something"     # namespaced: groups related probes under "mymodule.*"
        version = 1
        order = 50                    # optional; lower runs earlier (default 100)
        def applies_to(self, ctx): return bool(ctx.hosts)
        def run(self, ctx): return [contract.result(...)]

Optional hook: ``pre_snapshot(self, ctx)`` — if defined, the runner calls it on
every applicable probe *before* any probe's ``run``. Use it to capture a baseline
(e.g. NIC/UDP counters) so ``run`` can report a delta bracketing the whole
diagnostic pass.
"""

from __future__ import annotations

from typing import Any, Protocol, runtime_checkable

from framework.context import ProbeContext
from framework.contract import ProbeResult


@runtime_checkable
class Probe(Protocol):
    id: str
    version: int

    def applies_to(self, ctx: ProbeContext) -> bool: ...

    def run(self, ctx: ProbeContext) -> list[ProbeResult]: ...


_REGISTRY: list[Any] = []


def register(cls: type) -> type:
    """Class decorator that instantiates the probe once and registers the
    singleton. Duplicate ``id`` registration is rejected to keep namespaces clean."""
    instance = cls()
    pid = getattr(instance, "id", None)
    if not pid or "." not in str(pid):
        raise ValueError(f"Probe {cls.__name__} must define a namespaced 'id' like 'namespace.name'")
    if any(getattr(p, "id", None) == pid for p in _REGISTRY):
        raise ValueError(f"Duplicate probe id '{pid}' (registered by {cls.__name__})")
    _REGISTRY.append(instance)
    return cls


def all_probes() -> list[Any]:
    """Registered probes in run order (ascending ``order``, default 100)."""
    return sorted(_REGISTRY, key=lambda p: getattr(p, "order", 100))


def clear() -> None:
    """Test helper — empty the registry."""
    _REGISTRY.clear()
