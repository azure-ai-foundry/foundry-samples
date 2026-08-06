# Per-sample validation contract

`.github/scripts/validate-sample.sh` validates one sample at either L3 (load/build)
or L4 (credentialed execution). The default remains L3, so existing callers do not
need to change.

## CLI

```bash
# Existing L3 behavior
bash .github/scripts/validate-sample.sh \
  --language python \
  --sample-dir samples/python/quickstart/responses

# Opt-in L4 behavior
SKIP_PROVISION=true bash .github/scripts/validate-sample.sh \
  --level 4 \
  --sample-dir samples/python/quickstart/responses
```

Both modes use the same exit and verdict contract:

| Exit | Verdict | Meaning |
|---|---|---|
| `0` | `pass` | The requested check passed. In L4 mode, no declaration is also a clean no-op. |
| `1` | `fail` | The sample command/assertion failed. |
| `2` | `error` | The validator precondition or caller environment is invalid, or an L4 command explicitly reported caller/cloud infrastructure failure. |

See `CLASSIFICATION.md` for the authoritative failure-versus-error rules.

## `sample.yaml` L4 declaration

L4 is opt-in. A sample declares it with a top-level `l4` mapping:

```yaml
l4:
  command: >-
    python run_sample.py --assert-response
  required_env:
    - AZURE_OPENAI_ENDPOINT
    - MODEL_DEPLOYMENT
```

The contract is:

- `l4.command` is a required, non-empty shell string. The validator runs it from
  the sample directory with Bash and captures/preserves its output.
- The command owns a strict three-way result: exit `0` means pass, exit `1`
  means the sample/assertion failed, and exit `2` means a known caller/cloud
  infrastructure failure. Any other nonzero exit is conservatively classified
  as sample failure.
- The validator never infers L4 infrastructure failure from stdout/stderr text.
  A broken sample can legitimately print `503 Service Unavailable`, `Bad
  Gateway`, or similar application responses. If a command can distinguish a
  known credential, endpoint, or cloud transport failure, it must normalize
  that condition to exit `2`; ambiguous conditions must exit `1`.
- Do not expose a tool's raw exit code unless it already follows this contract.
  Common tools use `2` for sample-side conditions such as invalid arguments,
  interrupted tests, or usage errors. Wrap those commands so only a known
  caller/cloud infrastructure failure exits `2`; normalize other nonzero
  statuses to `1`.
- `l4.required_env` is optional. When present, it must be a list of valid
  environment-variable names. Every listed variable must be non-empty or the
  validator returns infrastructure error (`2`) before executing sample code.
- `SKIP_PROVISION` is a reserved caller input and must be set to exactly `true`
  or `false` whenever L4 is declared. The validator does not override it:
  trusted PR callers can use the warm project with `true`, while the cold
  cadence can use `false`.
- Authentication and cloud configuration are caller-owned. The command inherits
  the caller's environment and existing CLI/OIDC login. Do not put credentials,
  secrets, resource provisioning, or production mutations in `sample.yaml`.
- If `l4` is omitted (or `sample.yaml` itself is absent), `--level 4` exits `0`
  without requiring credentials or `SKIP_PROVISION`. It emits
  `l4_declared=false`; a declared check emits `l4_declared=true`.
- If `$GITHUB_OUTPUT` is set, `verdict` and `l4_declared` are appended there.
  `--results-dir` continues to write the sample path to
  `passed.txt`, `failed.txt`, or `errored.txt`.

The validator rejects a scalar `l4`, a missing/non-string/empty `command`, a
non-list `required_env`, invalid variable names, malformed YAML, and missing
declared environment inputs as infrastructure errors.
