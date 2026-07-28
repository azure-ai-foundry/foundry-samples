# ADO → GitHub Actions wrapper mapping (P1.4 retirement audit)

**Task:** ADO 5449688 — "P1.4 · Swap ADO task wrappers for GH actions"
(<https://msdata.visualstudio.com/Vienna/_workitems/edit/5449688>), parent Feature 5443705.

**Purpose.** This is the audit-of-record proving that the public-first validation path in this repo
carries **no Azure-Pipelines task wrappers or expressions** — every ADO construct from the private
port source has a first-class GitHub-Actions equivalent here, or was intentionally dropped with a
recorded reason. It also documents which constructs are deliberately **deferred to Phase 2** and
therefore intentionally absent from the Phase-1 foundation.

## Port source (read-only ground truth)

Private ADO pipeline: `microsoft-foundry/foundry-samples-pr` → `.azure-pipelines/validation.yml` @ `main`
(<https://github.com/microsoft-foundry/foundry-samples-pr/blob/main/.azure-pipelines/validation.yml>).
Its stages are **named, not numbered** — referred to by name below. (The file's own L199 comment
mislabels the `Validate` stage as "Stage 2"; that comment is ignored.)

| ADO stage (name) | Role | Ported to (Phase) |
| --- | --- | --- |
| `CheckMailmap` | commit-author mailmap gate | **DROPPED** (frozen plan) |
| `DetectChanges` | which samples changed | **P1.2** — `.github/scripts/detect-changed-samples.sh` |
| `Validate` | per-language L3 load validation | **P1.1** — `.github/scripts/validate-sample.sh` |
| `Report` | status posting / PR comment / manifest | **DROPPED**; returns as P2.3/P3 via commit statuses |

## GitHub-Actions targets on this branch (`brandom-msft-fluffy-lamp`, stacked on P1.3 `01cdde17`)

- `.github/workflows/validate-sample-selftest.yml` — real-runner harness for the `Validate` port.
- `.github/workflows/detect-changed-samples-selftest.yml` — real-runner harness for the `DetectChanges` port,
  including the `needs.*.outputs` cross-job plumbing proof.
- `.github/scripts/validate-sample.sh` — the once-only validator that collapses the five duplicated ADO
  per-language jobs into one function set.
- `.github/scripts/detect-changed-samples.sh` — the in-job change detector.
- `.github/scripts/test/**` — hermetic unit proofs driven by the self-tests.

---

## Acceptance-criteria verdicts

| AC | Requirement | Verdict | Evidence |
| --- | --- | --- | --- |
| **AC1** | No ADO-task-specific wrappers remain in the validation path | ✅ PASS | grep of the entire `.github` tree for `@N` task refs, `task:`, `$[ ]`, `stageDependencies`, `dependsOn`, `pool:`/`vmImage:`, `condition: eq(...)`, `##vso[...]`, `$(Build.*)`/`$(System.*)`/`$(Agent.*)` → **zero matches** |
| **AC2** | Language setup via GH actions | ✅ PASS | all 5 languages pinned via first-class setup actions in `validate-sample-selftest.yml` (table below) |
| **AC3** | Cross-job data flows via `needs.*.outputs` | ✅ PASS | `detect-changed-samples-selftest.yml` proves `detect`→`consume` and `detect-docs-only`→`consume-docs-only` via `needs.detect.outputs.{has_changes,count,samples}`; no `stageDependencies`/`$[coalesce(...)]` remains |

---

## AC2 — language-setup wrapper mapping

Each ADO `Use*/*Tool@N` setup task → the first-class GitHub setup action, with the version **pinned**
(not incidental to the runner image). Line numbers are `validate-sample-selftest.yml`.

| Language | ADO wrapper (`validation.yml`) | GH-native equivalent (this branch) | Pinned version |
| --- | --- | --- | --- |
| .NET | `UseDotNet@2` (L220) | `actions/setup-dotnet@v4` (L36–39) | `8.0.x` |
| Python | `UsePythonVersion@0` (L330) | `actions/setup-python@v5` (L41–44) | `3.12` |
| Node/TS | `NodeTool@0` (L465) | `actions/setup-node@v4` (L46–49) | `20` |
| Java | `JavaToolInstaller@0` (L604) | `actions/setup-java@v4` (L51–55) | `temurin` `17` |
| Go | `GoTool@0` (L729) | `actions/setup-go@v5` (L57–60) | `1.22` |

> **`GoTool@0` "for yq installation" (L226/L336/L471/L611).** In the ADO `Validate` stage the non-Go
> lanes each pulled Go *only* to `go install` yq. That incidental dependency is dropped: yq is installed
> directly from the mikefarah release binary in one `run:` step (`validate-sample-selftest.yml` L62–67),
> so no lane needs Go unless it is actually validating Go samples.

---

## AC1 / AC3 — full construct mapping (wrapper → GH equivalent OR intentionally dropped/deferred)

| ADO construct (`validation.yml` line) | Kind | Disposition | GH equivalent / reason (file · symbol) |
| --- | --- | --- | --- |
| `pool: { vmImage: ubuntu-latest }` (L44–45) | agent pool | **Ported** | `runs-on: ubuntu-latest` — `validate-sample-selftest.yml` L32, `detect-changed-samples-selftest.yml` L33 |
| `checkout: self` (L62, L97, L215, …) | checkout | **Ported** | `actions/checkout@v4` — self-tests L34 / L35 (`fetch-depth: 0` for the detector's history walk) |
| `UseDotNet@2` (L220) | task | **Ported** | `actions/setup-dotnet@v4` — self-test L36 |
| `UsePythonVersion@0` (L330) | task | **Ported** | `actions/setup-python@v5` — self-test L41 |
| `NodeTool@0` (L465) | task | **Ported** | `actions/setup-node@v4` — self-test L46 |
| `JavaToolInstaller@0` (L604) | task | **Ported** | `actions/setup-java@v4` — self-test L51 |
| `GoTool@0` (L729, and L226/L336/L471/L611 "for yq") | task | **Ported** / incidental use dropped | `actions/setup-go@v5` — self-test L57; yq via `run:` (L62–67) |
| `Install yq` inline `Bash@3` (L234/L343/L478/…) | task | **Ported** | `run:` step downloading the mikefarah release binary — self-test L62–67 |
| `Bash@3` "Validate <lang> samples" ×5 (L313/L448/L587/L712/L829) | task | **Ported + collapsed** | one script `validate-sample.sh` (invoked `run: bash …/run-tests.sh`, self-test L78–79). The five byte-identical per-language ADO job bodies become one function set: `run_sample_yaml` + `default_validate_{csharp,python,typescript,java,go}` (each cites its ADO source line in-comment) |
| `Bash@3` "Detect changed samples" (L165) | task | **Ported** | `detect-changed-samples.sh` (driven by `run:` in the detect self-test) |
| `condition: eq(variables.hasCSharp,'true')` ×5 (L213/L323/L458/L597/L722) | runtime expr | **Ported → data-driven** | per-sample fan-out from `needs.detect.outputs.samples`; language derived from the sample path (`samples/<language>/…`), not a stage-level boolean. Phase-1 self-test proves the `needs.*.outputs` mechanism (`consume` job, L73–97) |
| `$[coalesce(stageDependencies.DetectChanges.…outputs[…])]` → `hasX` (L204–208) | cross-stage expr | **Ported → `needs.*.outputs`** | `needs.detect.outputs.{has_changes,count,samples}` — detect self-test L44–47 (job outputs) + L79–92 (downstream read). No `stageDependencies`/`coalesce` remains |
| `condition: and(eq(parameters.validateAll,false), ne(Build.Reason,'Schedule'))` (L167) / `or(…,'Schedule')` (L193) | runtime expr | **Deferred (P2)** | validate-all / schedule mode is a Phase-2 trigger concern; not part of the Phase-1 foundation. No ADO expression carried over |
| `publish: … artifact: ChangedSamples` (L195–196) | task | **Ported → job outputs** | detected set flows as `needs.detect.outputs.samples` (self-test), not a pipeline artifact |
| `publish: … artifact: Results{CSharp,…}` (L315/L450/L589/L714/L831) | task | **Deferred (P2)** | per-lane result artifacts (`actions/upload-artifact`) belong to the P2 fan-out; Phase-1 validator writes `passed/failed/errored.txt` via `--results-dir` (`validate-sample.sh` `emit_verdict`) |
| download `ChangedSamples` / `Results*` (L857–877) | task | **Deferred (P2)** | `actions/download-artifact` in the P2 aggregation lane — not Phase 1 |
| `$[stageDependencies.Validate.Validate*.result]` (L845–849) | cross-stage expr | **Deferred (P2)** | future aggregation reads `needs.<job>.result`/job outputs; **no aggregator/gate-shim job in Phase 1** (frozen) |
| `$(Build.Reason)` / `$(Build.SourcesDirectory)` / `$(Build.ArtifactStagingDirectory)` (L71/L922/L195/…) | predefined var | **Ported where present / else deferred** | GH contexts: `$GITHUB_EVENT_NAME`, `$GITHUB_WORKSPACE` (used in the detect self-test L54), runner temp. No ADO `$( )` macro remains on this branch |
| `##vso[task.logissue type=error]` (L83–84/L900/L911/L938/L962/L985) | logging cmd | **Dropped with stages** | belonged to `CheckMailmap` + `Report`; both dropped. Phase-1 scripts fail loud via non-zero exit + plain stderr (`validate-sample.sh` `error()`, `detect-changed-samples.sh` fail-loud invariant) |

---

## Intentionally dropped (frozen plan) — NOT a gap

| ADO stage / step (line) | Why dropped |
| --- | --- |
| `CheckMailmap` stage (L56–87) | mailmap author-gating is not part of the public PR validation path (frozen). |
| `Report` stage — `Bash@3` mint GitHub-App token (L881) + publish statuses (L915) | status-posting is removed from the Phase-1 foundation; it returns as **P2.3/P3** via GitHub **commit statuses**, not an ADO task. Reintroducing token minting here would pull credentials into a credential-free harness (self-tests are `permissions: contents: read`). |
| `Report` — publish `ValidationSummary` / PR-comment (L1169–1175) | PR-comment surface is a Phase-2/3 reporting concern. |
| `Report` — publish `ValidationManifest` (L1343–1344, non-PR L1391) | manifest/roll-up is a Phase-2 aggregation concern. |

## Frozen constraints honored

No `pull_request_target`. No trust-scoring, aggregator/gate-shim jobs, or org-membership API calls.
The (future) trusted lane is **not** converted into a per-language matrix. These are P2 concerns and stay
outside the Phase-1 foundation.

## Residual gap

**None.** The literal ADO→GH swap was completed natively during P1.1/P1.2 authoring. This audit found no
Azure-Pipelines wrapper or expression remaining in the validation path; the only ADO constructs not
represented here are the intentional drops and Phase-2 deferrals recorded above. Proven additionally by a
green real-runner self-test run (recorded in the P1.4 close-out packet, not embedded here).
