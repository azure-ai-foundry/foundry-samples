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

The current inventory contains 72 samples:

- 61 samples have supported build-readiness validators.
- 11 Rust samples emit explicit `skipped/not-completed` records because build
  readiness does not yet support Rust.
- JavaScript samples use the existing TypeScript/node validator.
- Two samples opt in to live-service validation with
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

Discovery validates sample metadata and fails closed for malformed metadata,
duplicate identities, or unsafe sample paths. It also rejects the legacy `l4`
key with migration guidance.

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

Cadence evidence is available in the
[corrected manual run](https://github.com/microsoft-foundry/foundry-samples/actions/runs/31447067783)
and the
[first scheduled run](https://github.com/microsoft-foundry/foundry-samples/actions/runs/31469044031).

The implementation and reporting contract are defined in:

- [`.github/scripts/discover-validation-samples.py`](scripts/discover-validation-samples.py)
- [`.github/scripts/run-validation-pilot.py`](scripts/run-validation-pilot.py)
- [`.github/scripts/validate-validation-pilot-results.py`](scripts/validate-validation-pilot-results.py)
- [`.github/scripts/render-validation-report.py`](scripts/render-validation-report.py)
- [`.github/workflows/validation-report.yml`](workflows/validation-report.yml)
