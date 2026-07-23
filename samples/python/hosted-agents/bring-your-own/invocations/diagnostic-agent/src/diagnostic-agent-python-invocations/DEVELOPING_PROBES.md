# Developing Probes

This diagnostic agent is built around a small **probe framework** so that anyone
can add a new diagnostic without touching the request handler, the runner,
the aggregator, or the response schema. This guide shows how.

## The one rule

> A probe is a self-registering class that emits one or more uniform
> `ProbeResult` objects. Everything downstream — isolation, the top-level
> `summary`, the legacy `checks{}` view, schema validation — happens for free.

Because every probe emits the **same** envelope, adding a probe is
**Open/Closed**: no existing file changes except dropping your module into
`probes/`.

## Architecture at a glance

```
request → ProbeContext.from_spec → runner.run_all(registry) → report.build_report → JSON
                                        │
                                        ├─ pass 1: probe.pre_snapshot(ctx)   (optional baselines)
                                        └─ pass 2: probe.run(ctx)            (isolated per probe)
```

| Module | Responsibility (SRP) |
|---|---|
| `framework/contract.py` | The stable `ProbeResult` / `Finding` envelope, enums, and builders. **Do not** change this to add a probe. |
| `framework/context.py` | `ProbeContext` — per-invocation config + a shared `cache` dict. |
| `framework/registry.py` | `@register` + `Probe` protocol. Discovery only. |
| `framework/runner.py` | Runs probes with isolation. Probe-agnostic. |
| `framework/aggregator.py` | Rolls results into `summary`. Probe-agnostic. |
| `framework/report.py` | Assembles the envelope. |
| `framework/probelib.py` | Reusable stdlib network primitives (DNS/TCP/TLS/HTTP, `/proc` readers). |
| `framework/net_probe.py` | Stdlib raw-DNS engine. |
| `probes/` | The probes themselves. **Add your module here.** |

Only `main.py` lives in the base folder; everything else is under `framework/`
(shared framework + libraries) or `probes/` (the probes).

## Write a probe

Create `probes/example_thing.py`:

```python
import time
from framework.context import ProbeContext
from framework.contract import ProbeResult, Severity, Status, finding, result, status_from_findings
from framework.registry import register


@register
class ExampleThingProbe:
    id = "example.thing"    # MUST be namespaced "namespace.name" — groups related probes under "example.*"
    version = 1             # bump when your result shape changes
    order = 50              # optional; lower runs earlier (default 100)

    def applies_to(self, ctx: ProbeContext) -> bool:
        # Return False to skip cheaply (e.g. when no relevant input was supplied).
        return bool(ctx.hosts)

    def run(self, ctx: ProbeContext) -> list[ProbeResult]:
        t0 = time.perf_counter()
        findings = []
        # ... do your check (stdlib only!) ...
        if something_bad:
            findings.append(finding(
                "MY_PROBLEM_CODE", Severity.WARNING,
                "Human-readable explanation of what was observed.",
                remediation="What the operator should do about it.",
            ))
        return [
            result(
                self.id, self.version,
                status_from_findings(findings, default=Status.OK),
                target={"host": "..."},          # {host} | {url} | {target} | {kind}
                summary="one-line result",
                findings=findings,
                metrics={"some_number": 1.0},    # flat numerics for dashboards
                evidence={"raw": "..."},          # verbose detail (gated by include_evidence)
                elapsed_ms=round((time.perf_counter() - t0) * 1000, 1),
            )
        ]
```

Then register it for discovery by adding one line to `probes/__init__.py`:

```python
from . import example_thing  # noqa: F401
```

That's it. Your probe now runs, is isolated on failure, contributes to `summary`,
and validates against the schema.

## The envelope (what `result(...)` produces)

| Field | Meaning |
|---|---|
| `probe` | Your namespaced id. |
| `probe_version` | Your version integer. |
| `status` | `ok` \| `warn` \| `fail` \| `error` \| `skipped`. `error` is reserved for the runner (a crash); **you** use `ok`/`warn`/`fail`/`skipped`. |
| `target` | What the result is about: `{"host": ...}`, `{"url": ...}`, `{"target": ...}`, or `{"kind": "container"}`. |
| `summary` | One human-readable line. |
| `findings` | Discrete issues, each with a stable `code`, `severity`, `message`, optional `remediation`. This is the machine-readable "what's wrong". |
| `metrics` | **Flat** numeric map for dashboards/time-series (no nesting). |
| `evidence` | Verbose raw detail. Stripped when `include_evidence=false`, so never put a *finding* here. |
| `elapsed_ms` | How long your probe took. |

Guidance:
- Derive `status` from findings with `status_from_findings(...)` unless you have a
  reason to set it explicitly.
- Use **stable** `code` strings — consumers and dashboards key on them.
- Keep `metrics` flat and numeric; put structured/raw data in `evidence`.
- Emit **multiple** results from one `run` when you examine multiple targets
  (return a list).

## Capturing a baseline (delta metrics)

If your probe needs a "before" snapshot bracketing the whole diagnostic pass
(e.g. counters), implement the optional `pre_snapshot` hook. The runner calls it
on every applicable probe **before** any probe's `run`:

```python
    def pre_snapshot(self, ctx: ProbeContext) -> None:
        ctx.cache["example.baseline"] = read_counters()

    def run(self, ctx: ProbeContext) -> list[ProbeResult]:
        before = ctx.cache.get("example.baseline") or {}
        after = read_counters()
        # ... report the delta ...
```

See `probes/net_counters.py` for a complete example.

## Isolation & safety

- Any exception your `run` raises is caught by the runner and converted to a
  single `status="error"` result — it never aborts sibling probes and the
  response is still HTTP 200. You do **not** need a top-level try/except.
- **Stdlib only** in probe code. The network is what we're diagnosing; an
  import-time `pip` dependency can mask the very failure you're chasing. Reuse
  `probelib` / `net_probe` for network I/O.
- **No secrets.** If you surface environment or config, follow the redaction in
  `probelib.redact_env_value` (values matching `KEY`/`SECRET`/`TOKEN`/… become
  length-only).

## Test your probe

Run the hermetic contract suite (stdlib `unittest`, no network):

```bash
cd src/diagnostic-agent-python-invocations
python -m unittest discover -s tests -v
```

`tests/test_framework.py` validates the registry, isolation, ordering,
`pre_snapshot`, the rollup, the legacy projection, and (if `jsonschema` is
installed) the response against `schema/report.schema.json`. Add a focused test
for your probe's finding logic there.

## Versioning

- `schema_version` (in `contract.py`) is the **envelope** contract — bump only on
  a backward-incompatible change to the shared shape.
- `probe_version` is **yours** — bump when your result/metrics shape changes so
  consumers can adapt.
