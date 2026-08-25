# Daily public validation cadence

The [Daily public validation cadence](workflows/validation-pilot.yml) runs every
day at 07:00 UTC. Superseded runs are cancelled through workflow concurrency.
Maintainers can also run it from **Actions** > **Daily public validation
cadence** > **Run workflow**.

## Inventory and validation modes

At the checked-out commit, the workflow deterministically discovers every
`samples/**/sample.yaml` and generates the manifest and job matrices used by
that run. There is no static matrix to maintain: a new
`samples/**/sample.yaml` file is discovered automatically.

The current inventory contains 74 samples:

- 55 samples are eligible for execution in the current cadence.
- 19 samples emit explicit `skipped/not-completed` records: 12 have invalid
  YAML metadata and 7 use unsupported Rust build readiness.
- JavaScript samples use the existing TypeScript/node validator.
- Two eligible samples opt in to live-service validation with
  `live_service_validation` metadata.

**Build readiness** is credential-free. It uses a sample's declared build,
validate, or test command when present; otherwise, it uses the language-default
restore, build, compile, or syntax check.

**Live-service validation** is an opt-in, sample-owned runtime assertion. The
workflow runs build readiness first and proceeds to the declared live-service
command only when readiness passes. Authentication and configuration come from
the caller, and the workflow preserves the current `SKIP_PROVISION=true`
warm-project policy. The GitHub environment remains named `L4-validation`
because that legacy external identifier is part of the Entra OIDC subject; it
is not a current validation mode name.

Discovery uses the pinned
[`PyYAML`](scripts/requirements.txt) parser for the metadata fields it consumes.
Unreadable or malformed metadata stays in inventory as an explicit
`skipped/not-completed` record with an actionable reason. Ambiguous duplicate
derived IDs and metadata paths that do not resolve to regular files inside the
repository fail discovery before outputs are constructed. Discovery also
rejects the legacy `l4` key with migration guidance.

See the [per-sample validation contract](scripts/validate-sample.README.md) for
the language-by-language build-readiness matrix, metadata contract, and local
command examples. The runner modes are `--mode build-readiness` and
`--mode live-service`.

## Results and artifacts

Producers emit schema-v2 manifests and results. Completeness and reporting
readers retain schema-v1 compatibility for historical artifacts. New results
use descriptive completed stages such as `build readiness validation` and
`live-service validation`.

Every result uses one of these outcomes:

- `passed`
- `sample failure`
- `infrastructure/error`
- `skipped/not-completed`

A `sample failure` is valid reported data. Missing, duplicate, malformed, or
incomplete result artifacts violate the reporting contract and fail the run.

Each run publishes:

- One `validation-pilot-{sample-id}` artifact per discovered sample, containing
  `sample-result.json` and `diagnostics.log`.
- The generated manifest in `validation-pilot-manifest`.
- The consolidated
  `validation-pilot-run-{run_id}-{run_attempt}` artifact.
- A same-run summary in the `report / report` job.

## Interpret the report

The run-scoped report puts records requiring maintainer action first, followed
by informational skips and a compact passed section. It includes fleet counts,
links each sample to the validated commit, shows bounded and sanitized
diagnostic excerpts, and preserves visible skip reasons. Full logs remain in
the run artifacts.

The authoritative hardened-cadence evidence is
[run 32821260320](https://github.com/microsoft-foundry/foundry-samples/actions/runs/32821260320):
all 77 jobs succeeded and the run published 76 artifacts at commit
`b9b2cdd67efee6287e4b263f83ed45f18fe892be`: 74 per-sample
artifacts, the generated manifest, and the consolidated run artifact.

This is a current cadence snapshot, not a completed-workstream claim.
Additional live-service onboarding, cold provisioning, quarantine automation,
and Rust build-readiness support remain pending.

The implementation and reporting contract are defined in:

- [`.github/scripts/discover-validation-samples.py`](scripts/discover-validation-samples.py)
- [`.github/scripts/run-validation-pilot.py`](scripts/run-validation-pilot.py)
- [`.github/scripts/validate-validation-pilot-results.py`](scripts/validate-validation-pilot-results.py)
- [`.github/scripts/render-validation-report.py`](scripts/render-validation-report.py)
- [`.github/workflows/validation-report.yml`](workflows/validation-report.yml)
