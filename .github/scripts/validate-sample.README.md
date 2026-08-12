# Per-sample validation contract

`.github/scripts/validate-sample.sh` validates one sample for either build readiness
or live-service behavior. Build readiness is the default.

## CLI

```bash
# Build readiness (default)
bash .github/scripts/validate-sample.sh \
  --language python \
  --sample-dir samples/python/quickstart/responses

# Opt-in live-service validation
SKIP_PROVISION=true bash .github/scripts/validate-sample.sh \
  --mode live-service \
  --sample-dir samples/python/quickstart/responses
```

These commands invoke individual validation modes; calling `--mode live-service`
directly does not implicitly run build readiness first. The repository validation
pilot provides the end-to-end sequence: every supported sample runs build readiness,
and a sample with a `live_service_validation` declaration proceeds to live-service
validation only after readiness passes. Declaring live-service validation therefore
does not opt a sample out of build readiness.

## Build readiness by language

If `sample.yaml` declares `build`, `validate`, or `test` commands, the validator
runs those commands in that order instead of the language default below. The
first nonzero command fails build readiness.

| Sample language | Default build-readiness checks |
|---|---|
| C# | Run `dotnet build` for every `.csproj` in the sample directory. If none exists, report readiness as passed. |
| Python | Create an isolated virtual environment, install `requirements.txt` when present, and run `python -m py_compile` on top-level `.py` files. |
| TypeScript | If `package.json` exists, run `npm install` and `npm run build --if-present`. Otherwise, syntax-check top-level `.js` files with `node --check`; top-level `.ts` files have no default check without a package definition. |
| JavaScript | Use the TypeScript/Node.js validation path described above. |
| Java | Run `mvn compile` for Maven samples or a Gradle build for Gradle samples, preferring the sample's `gradlew` wrapper. If no supported build file exists, report readiness as passed. |
| Go | Run `go build ./...` for a module, or build each top-level `.go` file when no `go.mod` exists. |
| Rust | Not currently supported by build readiness. Discovered Rust samples produce explicit `skipped/not-completed` results. |

These defaults are credential-free readiness checks; they do not assert that a
sample can successfully call a live service.

Both modes use the same exit and verdict contract:

| Exit | Verdict | Meaning |
|---|---|---|
| `0` | `pass` | The requested check passed. In live-service mode, no declaration is also a clean no-op. |
| `1` | `fail` | The sample command/assertion failed. |
| `2` | `error` | The validator precondition or caller environment is invalid, or a live-service command explicitly reported caller/cloud infrastructure failure. |

See `CLASSIFICATION.md` for the authoritative failure-versus-error rules.

## `sample.yaml` live-service declaration

Live-service validation is opt-in. A sample declares it with a top-level
`live_service_validation` mapping:

```yaml
live_service_validation:
  command: >-
    python run_sample.py --assert-response
  required_env:
    - AZURE_OPENAI_ENDPOINT
    - MODEL_DEPLOYMENT
```

The contract is:

- `live_service_validation.command` is a required, non-empty shell string. The validator runs it from
  the sample directory with Bash and captures/preserves its output.
- The command owns a strict three-way result: exit `0` means pass, exit `1`
  means the sample/assertion failed, and exit `2` means a known caller/cloud
  infrastructure failure. Any other nonzero exit is conservatively classified
  as sample failure.
- The validator never infers live-service infrastructure failure from stdout/stderr text.
  A broken sample can legitimately print `503 Service Unavailable`, `Bad
  Gateway`, or similar application responses. If a command can distinguish a
  known credential, endpoint, or cloud transport failure, it must normalize
  that condition to exit `2`; ambiguous conditions must exit `1`.
- Do not expose a tool's raw exit code unless it already follows this contract.
  Common tools use `2` for sample-side conditions such as invalid arguments,
  interrupted tests, or usage errors. Wrap those commands so only a known
  caller/cloud infrastructure failure exits `2`; normalize other nonzero
  statuses to `1`.
- `live_service_validation.required_env` is optional. When present, it must be a list of valid
  environment-variable names. Every listed variable must be non-empty or the
  validator returns infrastructure error (`2`) before executing sample code.
- `SKIP_PROVISION` is a reserved caller input and must be set to exactly `true`
  or `false` whenever live-service validation is declared. The validator does not override it:
  trusted PR callers can use the warm project with `true`, while the cold
  cadence can use `false`.
- Authentication and cloud configuration are caller-owned. The command inherits
  the caller's environment and existing CLI/OIDC login. Do not put credentials,
  secrets, resource provisioning, or production mutations in `sample.yaml`.
- If `live_service_validation` is omitted (or `sample.yaml` itself is absent),
  `--mode live-service` exits `0`
  without requiring credentials or `SKIP_PROVISION`. It emits
  `live_service_validation_declared=false`; a declared check emits
  `live_service_validation_declared=true`.
- If `$GITHUB_OUTPUT` is set, `verdict` and `live_service_validation_declared`
  are appended there.
  `--results-dir` continues to write the sample path to
  `passed.txt`, `failed.txt`, or `errored.txt`.

The validator rejects the legacy `l4` key with a migration message. It also rejects
a scalar `live_service_validation`, a missing/non-string/empty `command`, a non-list
`required_env`, invalid variable names, malformed YAML, and missing declared
environment inputs as infrastructure errors.
