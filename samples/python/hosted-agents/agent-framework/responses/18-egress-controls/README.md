# Network egress controls (preview) for hosted agents

An [Agent Framework](https://github.com/microsoft/agent-framework) agent hosted on Microsoft Foundry using the **Responses protocol**. This sample demonstrates **network egress controls (preview)** for hosted agents by exposing a local `probe_egress` tool that performs an outbound HTTPS GET and returns the exact result. Use it to see the difference between egress policy **Audit** mode, where requests flow but decisions are logged, and **Enforce** mode, where denied requests are blocked.

## How it works

The agent uses `FoundryChatClient` from Agent Framework and is served with `ResponsesHostServer`. The `probe_egress` tool resolves the destination host, makes an HTTPS GET, and returns the raw status, body snippet, or exception. See [main.py](src/agent-framework-egress-controls-responses/main.py) for the implementation.

Network egress controls are configured in a Responsible AI (RAI) policy attached to the hosted agent. The policy can use ordered FQDN host-match rules with `Allow`, `Deny`, `Transform`, or `Rewrite` actions. In **Audit** mode, would-deny decisions are logged but the request is allowed. In **Enforce** mode, deny decisions are enforced by the egress proxy.

## Prerequisites

1. **Azure Developer CLI (`azd`)** — [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd).
2. Install the AI agent extension and authenticate:

   ```bash
   azd ext install azure.ai.agents
   azd auth login
   ```

3. An RAI policy on your Foundry resource with an `egressPolicy`. For authoring details, see [Add guardrails to a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/add-hosted-agent-guardrails) and the REST example [PutRaiPolicyWithEgress.json](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/cognitiveservices/resource-manager/Microsoft.CognitiveServices/preview/2026-05-15-preview/examples/PutRaiPolicyWithEgress.json). The documentation change for this preview is tracked in [MicrosoftDocs/azure-ai-docs-pr#13045](https://github.com/MicrosoftDocs/azure-ai-docs-pr/pull/13045).

## Initialize the agent project

No cloning required. Create a new folder and initialize from this sample manifest:

```bash
mkdir my-egress-controls-agent && cd my-egress-controls-agent

azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/responses/18-egress-controls/azure.yaml
```

Follow the prompts to configure your Foundry project and model deployment. If you don't have an existing Foundry project, `azd ai agent init` guides you through creating one.

## Attach an egress RAI policy

Set `raiPolicyName` in the generated [azure.yaml](azure.yaml) to your RAI policy's full ARM resource ID:

```yaml
policies:
  - type: rai_policy
    raiPolicyName: /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.CognitiveServices/accounts/<account>/raiPolicies/<policy-name>
```

Start with an Audit policy so you can observe the agent's outbound calls before blocking anything. Create or update the RAI policy with `egressPolicy.mode` set to `Audit` and rules such as this allowlist pattern:

```json
{
  "properties": {
    "mode": "Blocking",
    "basePolicyName": "Microsoft.DefaultV2",
    "egressPolicy": {
      "mode": "Audit",
      "defaultAction": "Deny",
      "rules": [
        {
          "name": "allow-example",
          "ruleType": "Fqdn",
          "match": { "host": "example.com" },
          "action": { "actionType": "Allow" }
        }
      ]
    }
  }
}
```

After reviewing the logged decisions and confirming the allowlist is complete, update the same policy to `"mode": "Enforced"` and redeploy or update the agent version that references it.

## Deploy and test

Provision Azure resources if needed:

```bash
azd provision
```

Deploy to Microsoft Foundry:

```bash
azd deploy
```

Invoke an allowed destination:

```bash
azd ai agent invoke "Probe https://example.com and return the raw result."
```

Invoke a destination that your policy denies:

```bash
azd ai agent invoke "Probe https://www.bing.com and return the raw result."
```

When a request is blocked in **Enforce** mode, the egress proxy returns `HTTP 403` to the agent's network client. This sample surfaces that directly in the tool output, for example:

```text
PROBE url=https://www.bing.com | DNS host=www.bing.com -> 204.79.197.200 | HTTP_ERROR status=403 reason=Forbidden body='...' | elapsed_ms=123
```

In **Audit** mode, the same request is allowed to complete, but the egress decision is logged for review.

## Run locally

You can run the agent locally before deployment, but Foundry network egress controls are enforced only in the hosted runtime after the RAI policy is attached.

```bash
azd ai agent run
azd ai agent invoke --local "Probe https://example.com and return the raw result."
```

## Next steps

- [Add guardrails to a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/add-hosted-agent-guardrails) — attach RAI policies, including network egress controls, to hosted agents.
- [PutRaiPolicyWithEgress.json](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/cognitiveservices/resource-manager/Microsoft.CognitiveServices/preview/2026-05-15-preview/examples/PutRaiPolicyWithEgress.json) — REST payload example for `egressPolicy`.
- [Deploy a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/deploy-hosted-agent) — full deployment guide.
- [Agent with local tools](../02-tools/) — learn the local tool pattern used by this sample.
