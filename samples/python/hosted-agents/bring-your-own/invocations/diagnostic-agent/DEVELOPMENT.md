# Development and validation

This document is for sample maintainers adding diagnostic probes or validating response and deployment-mode compatibility. For customer networking triage, see [README.md](README.md).

## Developing probes

This diagnostic agent uses a small probe framework so a new diagnostic does not require changes to the request handler, runner, aggregator, or response schema.

### The one rule

A probe is a self-registering class that emits one or more uniform `ProbeResult` objects. The framework handles isolation, the top-level `summary`, the legacy `checks{}` view, and schema validation.

### Architecture

```text
request -> ProbeContext.from_spec -> runner.run_all(registry) -> report.build_report -> JSON
                      |
                      |-- pass 1: probe.pre_snapshot(ctx) (optional baselines)
                      `-- pass 2: probe.run(ctx)          (isolated per probe)
```

| Module | Responsibility |
|---|---|
| `src/diagnostic-agent-python-invocations/framework/contract.py` | Stable `ProbeResult` and `Finding` envelope, enums, and builders. Do not change this to add a probe. |
| `src/diagnostic-agent-python-invocations/framework/context.py` | Per-invocation configuration and shared `cache`. |
| `src/diagnostic-agent-python-invocations/framework/registry.py` | `@register` and the `Probe` protocol. |
| `src/diagnostic-agent-python-invocations/framework/runner.py` | Probe execution and isolation. |
| `src/diagnostic-agent-python-invocations/framework/aggregator.py` | Summary rollup. |
| `src/diagnostic-agent-python-invocations/framework/report.py` | Response-envelope assembly. |
| `src/diagnostic-agent-python-invocations/framework/probelib.py` | Reusable stdlib network primitives. |
| `src/diagnostic-agent-python-invocations/framework/net_probe.py` | Stdlib raw-DNS engine. |
| `src/diagnostic-agent-python-invocations/probes/` | Probe implementations. Add new probe modules here. |

### Write a probe

Create a module such as `src/diagnostic-agent-python-invocations/probes/example_thing.py`:

```python
import time

from framework.context import ProbeContext
from framework.contract import ProbeResult, Severity, Status, finding, result, status_from_findings
from framework.registry import register


@register
class ExampleThingProbe:
  id = "example.thing"
  version = 1
  order = 50

  def applies_to(self, ctx: ProbeContext) -> bool:
    return bool(ctx.hosts)

  def run(self, ctx: ProbeContext) -> list[ProbeResult]:
    started = time.perf_counter()
    findings = []
    if something_bad:
      findings.append(
        finding(
          "MY_PROBLEM_CODE",
          Severity.WARNING,
          "Human-readable explanation of what was observed.",
          remediation="What the operator should do about it.",
        )
      )
    return [
      result(
        self.id,
        self.version,
        status_from_findings(findings, default=Status.OK),
        target={"host": "..."},
        summary="one-line result",
        findings=findings,
        metrics={"some_number": 1.0},
        evidence={"raw": "..."},
        elapsed_ms=round((time.perf_counter() - started) * 1000, 1),
      )
    ]
```

Register it for discovery in `src/diagnostic-agent-python-invocations/probes/__init__.py`:

```python
from . import example_thing  # noqa: F401
```

Probe IDs must be namespaced, such as `example.thing`. Lower `order` values run earlier; the default is 100. Increment `version` when the probe's result or metrics shape changes.

### Result envelope

| Field | Meaning |
|---|---|
| `probe` | Namespaced probe ID. |
| `probe_version` | Probe version. |
| `status` | `ok`, `warn`, `fail`, `error`, or `skipped`. `error` is reserved for runner-caught crashes. |
| `target` | Subject of the result, such as a host, URL, target, or container. |
| `summary` | One human-readable line. |
| `findings` | Issues with stable code, severity, message, and optional remediation. |
| `metrics` | Flat numeric values for dashboards and time series. |
| `evidence` | Verbose details removed when `include_evidence=false`. Do not put findings here. |
| `elapsed_ms` | Probe duration. |

- Derive status with `status_from_findings(...)` unless it must be set explicitly.
- Keep finding codes stable because consumers may key on them.
- Keep metrics flat and numeric; put structured data in evidence.
- Return multiple results when examining multiple targets.

### Capture a baseline

For delta metrics, implement `pre_snapshot`. The runner invokes it on every applicable probe before running any probe:

```python
def pre_snapshot(self, ctx: ProbeContext) -> None:
  ctx.cache["example.baseline"] = read_counters()

def run(self, ctx: ProbeContext) -> list[ProbeResult]:
  before = ctx.cache.get("example.baseline") or {}
  after = read_counters()
  # Report the delta.
```

See `src/diagnostic-agent-python-invocations/probes/net_counters.py` for a complete example.

### Isolation and safety

- The runner converts an exception from `run` into one `status="error"` result. It does not abort sibling probes, and the response remains HTTP 200.
- Use only the standard library in probe code. Reuse `probelib` and `net_probe` for network I/O.
- Do not expose secrets. Follow `probelib.redact_env_value` when reporting environment or configuration values.

### Test a probe

Run the hermetic contract suite:

```bash
cd src/diagnostic-agent-python-invocations
python -m unittest discover -s tests -v
```

`tests/test_framework.py` covers registry behavior, isolation, ordering, `pre_snapshot`, rollup, legacy projection, and response-schema validation when `jsonschema` is installed. Add focused tests for new finding logic there.

### Versioning

- `schema_version` in `framework/contract.py` identifies the shared envelope. Increment it only for a backward-incompatible envelope change.
- `probe_version` identifies an individual probe result. Increment it when that probe's result or metrics shape changes.

## Verify buffered and streaming responses

Run the same payload after each deployment mode so the reports are directly comparable:

```bash
INVOCATIONS_URL="https://<account>.services.ai.azure.com/api/projects/<project>/agents/<agent>/endpoint/protocols/invocations?api-version=v1"
TOKEN=$(az account get-access-token --resource https://ai.azure.com --query accessToken -o tsv)
PAYLOAD='{"hosts":["<acr>.azurecr.io"],"public_hosts":[],"raw_dns":false,"dns_propagation_probe":true,"dns_propagation_duration_sec":20,"dns_propagation_interval_sec":1,"dns_propagation_threshold_sec":15}'

# Buffered JSON
curl -sS -X POST "$INVOCATIONS_URL" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "$PAYLOAD" > report.json

# SSE: heartbeats followed by the same report envelope
curl -sS -N -X POST "$INVOCATIONS_URL" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Accept: text/event-stream" \
  -d "${PAYLOAD%?},\"stream\":true}" > report.sse

# Extract the final report event for schema validation or comparison
jq -cR 'select(startswith("data: ")) | ltrimstr("data: ") | fromjson | select(.type == "report") | .report' \
  report.sse | jq > report-from-sse.json
```

## Verification matrix

| Deployment | Response | Expected result |
|---|---|---|
| Container image | Buffered JSON | One report document after all probes finish. |
| Container image | SSE | `started`, periodic `heartbeat`, `report`, and `done` events. |
| ZIP/code | Buffered JSON | Same report contract as container mode. |
| ZIP/code | SSE | Same SSE event sequence and final report contract as container mode. |

Compare diagnostic content after removing invocation-specific and timing fields:

```bash
jq 'del(.agent_session_id,.invocation_id,.timestamp_utc,.elapsed_ms) |
    .results |= map(del(.elapsed_ms) | .evidence.attempts? |= map(del(.timestamp_utc,.elapsed_sec)))' \
  report.json > report.normalized.json

jq 'del(.agent_session_id,.invocation_id,.timestamp_utc,.elapsed_ms) |
    .results |= map(del(.elapsed_ms) | .evidence.attempts? |= map(del(.timestamp_utc,.elapsed_sec)))' \
  report-from-sse.json > report-from-sse.normalized.json

diff -u report.normalized.json report-from-sse.normalized.json
```

DNS and network observations can legitimately change between sequential invocations. Compare the report schema, probe set, finding codes, and timing-probe classification rather than requiring byte-for-byte equality.