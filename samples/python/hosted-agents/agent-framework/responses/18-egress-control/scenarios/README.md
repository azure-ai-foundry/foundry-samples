# Egress Control Agent — Example Policy Scenarios

Runnable end-to-end **example scenarios** (not unit tests) that deploy egress policies, create agent versions, and invoke the agent to demonstrate and validate policy enforcement. Use them as worked examples you can adapt for your own egress policies.

## Prerequisites

- **Deployed agent** — the egress test agent must be deployed to a Foundry project
- **Azure CLI** — `az login` with access to the CogSvc account
- **Python 3.10+** with `pytest` installed

## Environment variables

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_ENDPOINT` | ✅ | Foundry project endpoint, e.g. `https://myaccount.services.ai.azure.com/api/projects/myproject` |
| `AGENT_NAME` | ✅ | Deployed agent name, e.g. `egress-test-af` |
| `COGSVC_ACCOUNT_ID` | ✅ | Full ARM resource ID, e.g. `/subscriptions/.../Microsoft.CognitiveServices/accounts/myaccount` |
| `AGENT_IMAGE` | ✅ | ACR image reference (digest or tag) |
| `MODEL_DEPLOYMENT` | | Model deployment name (default: `gpt-4.1`) |
| `RAI_BASE_POLICY` | | Base RAI policy name (default: `Microsoft.DefaultV2`) |
| `INVOKE_DELAY` | | Seconds between invocations to avoid rate limits (default: `15`) |
| `AGENT_STORAGE_ACCOUNT` | | Storage account name for MI tests (e.g. `myteststorage`) |
| `AGENT_STORAGE_CONTAINER` | | Blob container name for MI tests (default: `egress-test`) |

## Running the scenarios

```bash
# Set required environment variables
export FOUNDRY_ENDPOINT="https://myaccount.services.ai.azure.com/api/projects/myproject"
export AGENT_NAME="egress-test-af"
export COGSVC_ACCOUNT_ID="/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>"
export AGENT_IMAGE="myregistry.azurecr.io/egress-test-agent-framework@sha256:..."

# Install pytest
pip install pytest

# Run basic scenarios (scenarios 1–7)
pytest scenarios/scenario_basic.py -v --tb=short

# Run advanced scenarios (scenarios 8–12)
pytest scenarios/scenario_advanced.py -v --tb=short

# Run audit mode scenarios (scenarios 13–15)
pytest scenarios/scenario_audit.py -v --tb=short

# Run ManagedIdentityRef scenarios (scenarios 16–17) — requires AGENT_STORAGE_ACCOUNT
export AGENT_STORAGE_ACCOUNT="myteststorage"
pytest scenarios/scenario_managed_identity.py -v --tb=short

# Run all scenarios
pytest scenarios/ -v --tb=short
```

## Execution time

Each scenario class creates a policy, deploys a new agent version (waits ~60–90s for it to become active), runs invocations with delays to avoid rate limits, then cleans up. Expect **~3–5 minutes per scenario class**.

Full suite (17 scenario classes) takes approximately **60–90 minutes**.

## Scenario files

- `conftest.py` — shared helpers: token management, policy CRUD, agent version deployment, invocation, storage helpers
- `scenario_basic.py` — Scenarios 1–7: Allow/Deny, Transform (Insert/Set/Remove), Rewrite (Host/Path), connectivity baseline
- `scenario_advanced.py` — Scenarios 8–12: first-match ordering, multi-transform, rewrite+transform, deny-all, wildcard matching
- `scenario_audit.py` — Scenarios 13–15: audit mode deny passes, audit connectivity, audit vs enforced comparison
- `scenario_managed_identity.py` — Scenarios 16–17: MI token injection to storage, MI token host scoping
