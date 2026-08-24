# Sample validator classification contract

Authoritative `failure`-vs-`error` contract for `.github/scripts/validate-sample.sh`.
The public-first validation pipeline — and specifically the quarantine loop (P4.4) —
**keys on this contract**. Read this before changing the classifier or anything that
consumes its verdict.

## Exit contract (three-way, load-bearing)

| Exit | Verdict | Meaning | Downstream action |
|------|---------|---------|-------------------|
| `0`  | `pass`  | The requested mode passed: build readiness builds/compiles, or the declared live-service command exited 0. Live-service validation with no declaration is also a successful no-op. | none |
| `1`  | `fail`  | **The SAMPLE is broken**, or a runtime failure we cannot positively attribute to our infra. | P4.4 may count a strike / quarantine (advisory-first). |
| `2`  | `error` | **OUR INFRA is sick** — a precondition error, a dedicated dependency install with positively-identified transport failure, or a live-service command explicitly reporting caller/cloud infrastructure failure with exit 2. | Page us. **P4.4 must NEVER count or quarantine on `error`.** |

The split is a safety interlock. A real breakage mislabeled `error` hides broken code forever
(there is no counter behind `error`). A transient infra blip mislabeled `failure` risks
quarantining a healthy sample. Both directions are dangerous; keep the split honest.

## What is `error` (exit 2)

**Precondition errors (validator can't even run the check):**
- bad / missing CLI args; unknown or missing `--language`; missing or nonexistent `--sample-dir`
- `sample.yaml` unreadable or malformed (`yq` read fails)
- a declared `live_service_validation` block has an invalid shape, a required live-service environment variable is missing,
  or the caller did not set `SKIP_PROVISION` to exactly `true` or `false`
- a required toolchain binary missing from `PATH` (`require_tool`)
- `yq` unavailable when a `sample.yaml` must be read
- Python venv create/activate failure

**Runtime infrastructure failures:**
- a `pip install` or `npm install` that failed with **positive transport evidence**:
  DNS failure, connection refused/reset, connect/read timeout, or a registry `5xx`.
- a declared live-service command that explicitly exits `2` after identifying a known caller/cloud
  infrastructure failure (for example credential, endpoint, or cloud transport failure).
- Pip/npm detection lives in `dep_infra_signature()` — a **narrow allow-list of tool-specific
  transport strings** (e.g. `ECONNREFUSED`, `ENOTFOUND`, `Temporary failure in name
  resolution`, `Max retries exceeded`, `503 Server Error`). It is the authoritative signature
  list for those dedicated install logs only; arbitrary live-service output never enters it.

## What is `failure` (exit 1)

- any `sample.yaml` build/validate/test command exits non-zero
- a declared `sample.yaml` live-service command exits `1`
- a declared live-service command exits with any unexpected nonzero status other than explicit error `2`
- a live-service command exits `1` after printing transport-like application text such as
  `503 Service Unavailable`; text alone never upgrades it to infrastructure error
- a compile/build step fails: `dotnet build`, `mvn compile`, `gradle build`, `go build`,
  `npm run build`, `node --check`, `py_compile`
- a dependency install (`pip install` / `npm install`) fails **without** transport evidence —
  i.e. a genuine resolution error: package does not exist, no matching version, version
  conflict (`No matching distribution found`, npm `E404` / `ETARGET` / `ERESOLVE`).

## Ambiguity-bias rule

When a dependency-install failure shows **no** positive transport evidence, it is classified
`failure`, **never** `pass`. Live-service commands must normalize a known infrastructure condition to
exit `2`; output text is not interpreted. When we genuinely cannot tell an infra blip from a
sample break, we bias to **`failure`**.

Rationale — *recourse asymmetry*, not "strikes are cheap":
- A false `error` has **no counter**: it silently hides real breakage forever.
- A false `failure` is recoverable: P4.4 runs advisory-first, quarantine is strike-based, and a
  human reviews before a sample moves.
- We keep the transport allow-list **narrow** so false `failure`s stay rare in the first place —
  we do not lean on the strike mechanism to absorb sloppiness (infra outages are correlated and
  can survive multiple runs).

Fail-loud (ADO 5247751 lesson): an indeterminate probe must resolve to a defined verdict. It
never fails **open** to `pass`.

## Honest v1 limits

- Only the two **dedicated** dependency-install steps — `pip install` and `npm install` — are
  inspected for transport evidence. The **merged resolve+compile tools** (`dotnet build`,
  `mvn compile`, `gradle build`, `go build`, `npm run build`) interleave dependency download
  with compilation and user scripts; grepping their combined output would manufacture false
  `error`s, so in v1 they classify as `failure`. A network blip that strikes *during* one of
  those merged steps can therefore be misread as a sample `failure`. This is a known limit, not
  a bug — extending it needs tool-specific restore phases and is deferred.
- `dep_infra_signature()` is a **narrow heuristic**, not a robust classifier. It matches known
  transport strings and **will need maintenance** as runner `pip` / `npm` versions drift their
  error wording. Unknown output stays `failure`.
- **Blocked-registry / proxy `403`.** The transport allow-list catches connection-level failures
  (DNS, refused/reset, timeout) and registry `5xx`, but **not** an HTTP `403 Forbidden` or a
  blocked-page response from a policy proxy. This matters where a registry is deliberately fenced
  off — e.g. Microsoft Security is blocking direct access to public npm registries on
  Microsoft-managed devices, routing installs through a CFS-protected feed; a package still inside
  the release-hold window can come back `403`/blocked. Such a block is arguably *infra*, but in v1
  it classifies as `failure` (bias-to-`failure`, no false `error`). This is moot for the deliverable
  itself — the pipeline runs on GitHub-hosted runners, not managed devices — but bites local runs on
  managed devices. Promoting proxy-`403`/blocked-registry to `error` is deferred follow-up.

## Proof

The contract is exercised end-to-end on a real GitHub Actions runner by
`.github/scripts/test/run-tests.sh` (invoked from the `validate-harness` job in
`.github/workflows/scripts-selftest.yml`). It covers declared live-service pass, undeclared no-op,
explicit live-service fail/error exits, unexpected nonzero failure, transport-like text that remains a
failure, invalid declarations, missing caller environment, `GITHUB_OUTPUT`, and results files.
The dependency checks separately prove that a forced-unreachable registry
(`127.0.0.1:1` blackhole) classifies as `error` (exit 2), while an unaccompanied resolution
failure classifies as `failure` (exit 1).
