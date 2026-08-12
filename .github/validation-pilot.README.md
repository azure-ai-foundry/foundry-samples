# Daily public validation cadence

`validation-pilot.yml` runs daily at 07:00 UTC and can also be dispatched
manually. It discovers every `samples/**/sample.yaml` at the checked-out commit,
generates a deterministic manifest and matrix, and runs with `fail-fast: false`.
The first full-fleet run should be manually reviewed before the first scheduled
occurrence.

All supported samples run build readiness. Samples declaring live-service validation use a separate credentialed
matrix leg to avoid duplicate validation and unnecessary environment deployment
records, but that leg still runs build readiness first and proceeds to live-service
validation only when readiness passes. Declaring live-service validation never opts
a sample out of build readiness. JavaScript uses the existing
TypeScript/node validator mapping. Rust samples remain in the manifest and emit
`skipped/not-completed` with an explicit unsupported-language reason.

Each matrix leg persists `sample-result.json` and `diagnostics.log` in a
versioned artifact. Declared `sample.yaml` live-service commands run through the
existing `L4-validation` OIDC environment and warm-project seam. That environment
name is a legacy external GitHub/Entra identifier and is not the validation mode
name. P4.1 does not provision
resources and does not set a cold-provisioning default.

The result schema remains owned by the producer and includes
sample identity, one of `passed`, `sample failure`, `infrastructure/error`, or
`skipped/not-completed`, the completed stage, duration, references to the
diagnostic and result artifacts, completion time, and GitHub run metadata.

The completeness job fails the run if any discovered sample is missing,
duplicated, malformed, or missing its diagnostic. Individual sample failures
remain valid result records and do not prevent later matrix legs from running.
The generated manifest is included in the normalized run artifact so the
same-run report consumes exactly the inventory that was executed.
