# Egress Control Agent — .NET Example Policy Scenarios

Runnable end-to-end scenarios that deploy egress policies, create versions of
the .NET agent, and invoke it to validate policy enforcement. The harness is
written in Python because it exercises Azure control-plane and Responses APIs;
the agent under test is the C# application in this sample.

## Prerequisites

- The egress-control agent deployed to a Foundry project
- Azure CLI authenticated with access to the Foundry account
- Python 3.10+ with `pytest`

## Environment variables

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_ENDPOINT` | Yes | Foundry project endpoint |
| `AGENT_NAME` | Yes | Deployed agent name; default manifest name is `agent-framework-egress-control-responses-dotnet` |
| `COGSVC_ACCOUNT_ID` | Yes | Full ARM resource ID of the Foundry account |
| `AGENT_IMAGE` | Yes | ACR image reference for the .NET agent |
| `MODEL_DEPLOYMENT` | No | Model deployment name (default: `gpt-4.1`) |
| `RAI_BASE_POLICY` | No | Base RAI policy (default: `Microsoft.DefaultV2`) |
| `INVOKE_DELAY` | No | Delay between invocations (default: `15`) |
| `AGENT_STORAGE_ACCOUNT` | MI only | Storage account used by scenarios 16–17 |
| `AGENT_STORAGE_CONTAINER` | No | Blob container (default: `egress-test`) |
| `EGRESS_MI_ENABLED` | No | Set to `1` after managed-identity injection is available |

## Run the scenarios

```bash
export FOUNDRY_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AGENT_NAME="agent-framework-egress-control-responses-dotnet"
export COGSVC_ACCOUNT_ID="/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>"
export AGENT_IMAGE="<registry>.azurecr.io/<image>@sha256:<digest>"

python -m pip install pytest
pytest scenarios/scenario_basic.py -v --tb=short
pytest scenarios/scenario_advanced.py -v --tb=short
pytest scenarios/scenario_audit.py -v --tb=short
pytest scenarios/scenario_managed_identity.py -v --tb=short
```

Each scenario provisions a policy, deploys and activates a new agent version,
runs its commands, and removes the policy. Expect several minutes per scenario.
Agent versions are retained for diagnostics, so use a disposable test agent or
remove the generated versions after a run. The final retained version references
a policy that the scenario cleanup deletes.
Managed-identity scenarios are skipped by default because token injection is a
known platform limitation as of July 2026.

## Coverage

- `scenario_basic.py` — scenarios 1–7: Allow/Deny, header transforms, rewrites, connectivity
- `scenario_advanced.py` — scenarios 8–12: ordering, combined transforms, deny-all, wildcards
- `scenario_audit.py` — scenarios 13–15: audit passthrough and enforced comparison
- `scenario_managed_identity.py` — scenarios 16–17: storage token injection and host scoping
