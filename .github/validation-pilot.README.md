# Representative public validation pilot

`validation-pilot.yml` is a manual-only, non-gating workflow. It runs the
versioned matrix in `validation-pilot-matrix.json` with `fail-fast: false`:

| Sample | Language | Shape |
| --- | --- | --- |
| `csharp/quickstart/chat-with-agent` | C# | quickstart |
| `java/quickstart/create-agent` | Java | quickstart |
| `python/quickstart/chat-with-agent` | Python | quickstart with dependencies |
| `typescript/quickstart/chat-with-agent` | TypeScript | declared-command quickstart |

Each matrix leg persists `sample-result.json` and `diagnostics.log` in a
versioned artifact. The result schema is owned by the producer and includes
sample identity, one of `passed`, `sample failure`, `infrastructure/error`, or
`skipped/not-completed`, the completed stage, duration, references to the
diagnostic and result artifacts, completion time, and GitHub run metadata.

The completeness job fails the run if any matrix member is missing, duplicated,
malformed, or missing its diagnostic. Individual sample failures remain valid
result records and do not prevent later matrix legs from running.
