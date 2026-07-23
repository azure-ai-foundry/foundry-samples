@description('Name of the existing Azure AI Services (Foundry) account that hosts the agent.')
param accountName string

@description('Base RAI policy to inherit content filters from. A single RAI policy carries both content filters (content safety) and the egressPolicy, so every policy below inherits the same base filters.')
param basePolicyName string = 'Microsoft.DefaultV2'

@description('''Name of the policy — from the catalog provisioned below — that is attached to the deployed agent. Its ARM resource ID is exported as RAI_POLICY_ID and consumed by azure.yaml. The agent binds to a SINGLE RAI policy; to combine guardrails, add more rules to that policy rather than attaching more policies.''')
param agentPolicyName string = 'allow-httpbin'

@description('''Blob storage host used by the managed-identity injection policy, e.g. "mystorage.blob.core.windows.net". Leave empty (default) to skip provisioning the managed-identity policy, which requires the account to have a managed identity with Storage Blob Data RBAC.''')
param managedIdentityStorageHost string = ''

// ── Egress policy catalog ────────────────────────────────────────────────────
// Each entry provisions one RAI egress policy that demonstrates a distinct
// guardrail pattern. These mirror the end-to-end scenarios under scenarios/, but
// here they are declared declaratively so customers deploy the whole catalog and
// can attach any of them to an agent. Rules are evaluated first-match.

var corePolicies = [
  // Basics ───────────────────────────────────────────────────────────────────
  {
    // Deny all egress except httpbin.org — the canonical guardrail.
    name: 'allow-httpbin'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'allow-httpbin'
          ruleType: 'Fqdn'
          match: { host: 'httpbin.org' }
          action: { actionType: 'Allow' }
        }
      ]
    }
  }
  {
    // Transform → Insert a custom request header on allowed traffic.
    name: 'transform-insert-header'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'insert-header'
          ruleType: 'Fqdn'
          match: { host: 'httpbin.org' }
          action: {
            actionType: 'Transform'
            headers: [
              { name: 'X-Custom-Tag', value: 'my-value', operation: 'Insert' }
            ]
          }
        }
      ]
    }
  }
  {
    // Transform → Set (overwrite) an existing request header.
    name: 'transform-set-header'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'set-user-agent'
          ruleType: 'Fqdn'
          match: { host: 'httpbin.org' }
          action: {
            actionType: 'Transform'
            headers: [
              { name: 'User-Agent', value: 'policy-override-agent', operation: 'Set' }
            ]
          }
        }
      ]
    }
  }
  {
    // Transform → Remove a request header.
    name: 'transform-remove-header'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'remove-marker'
          ruleType: 'Fqdn'
          match: { host: 'httpbin.org' }
          action: {
            actionType: 'Transform'
            headers: [
              { name: 'X-Test-Marker', operation: 'Remove' }
            ]
          }
        }
      ]
    }
  }
  {
    // Rewrite → redirect a host (www.google.com → www.bing.com).
    name: 'rewrite-host'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'rewrite-to-bing'
          ruleType: 'Fqdn'
          match: { host: 'www.google.com' }
          action: {
            actionType: 'Rewrite'
            rewrite: { scheme: 'https', host: 'www.bing.com' }
          }
        }
      ]
    }
  }
  {
    // Rewrite → redirect a path (httpbin.org/get → httpbin.org/ip).
    name: 'rewrite-path'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'rewrite-path'
          ruleType: 'Fqdn'
          match: { host: 'httpbin.org', path: '/get' }
          action: {
            actionType: 'Rewrite'
            rewrite: { scheme: 'https', host: 'httpbin.org', path: '/ip' }
          }
        }
      ]
    }
  }
  {
    // Allow-all baseline — connectivity with no host restrictions.
    name: 'allow-all'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'allow-all'
          ruleType: 'Fqdn'
          match: { host: '*' }
          action: { actionType: 'Allow' }
        }
      ]
    }
  }
  // Advanced ───────────────────────────────────────────────────────────────────
  {
    // First-match priority — an earlier Deny beats a later broad Allow.
    name: 'first-match-priority'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'deny-httpbin-ip'
          ruleType: 'Fqdn'
          match: { host: 'httpbin.org', path: '/ip' }
          action: { actionType: 'Deny' }
        }
        {
          name: 'allow-httpbin-all'
          ruleType: 'Fqdn'
          match: { host: 'httpbin.org' }
          action: { actionType: 'Allow' }
        }
      ]
    }
  }
  {
    // Multiple header transforms in a single rule (Insert + Set + Remove).
    name: 'multi-transform'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'multi-transform'
          ruleType: 'Fqdn'
          match: { host: 'httpbin.org' }
          action: {
            actionType: 'Transform'
            headers: [
              { name: 'X-Custom-Inserted', value: 'hello', operation: 'Insert' }
              { name: 'User-Agent', value: 'policy-agent/1.0', operation: 'Set' }
              { name: 'X-Test-Marker', operation: 'Remove' }
            ]
          }
        }
      ]
    }
  }
  {
    // Rewrite and Transform combined as separate rules in one policy.
    name: 'rewrite-and-transform'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'rewrite-to-bing'
          ruleType: 'Fqdn'
          match: { host: 'www.google.com' }
          action: {
            actionType: 'Rewrite'
            rewrite: { scheme: 'https', host: 'www.bing.com' }
          }
        }
        {
          name: 'transform-httpbin'
          ruleType: 'Fqdn'
          match: { host: 'httpbin.org' }
          action: {
            actionType: 'Transform'
            headers: [
              { name: 'X-Policy-Tag', value: 'tagged', operation: 'Insert' }
            ]
          }
        }
      ]
    }
  }
  {
    // Deny-all — no rules, defaultAction Deny blocks every destination.
    name: 'deny-all'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: []
    }
  }
  {
    // Wildcard host — *.org matches httpbin.org but not example.com.
    name: 'wildcard-host'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'allow-dot-org'
          ruleType: 'Fqdn'
          match: { host: '*.org' }
          action: { actionType: 'Allow' }
        }
      ]
    }
  }
  // Audit mode ─────────────────────────────────────────────────────────────────
  {
    // Audit mode — deny rules are logged but not enforced (traffic passes).
    name: 'audit-deny'
    egressPolicy: {
      mode: 'Audit'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'allow-httpbin'
          ruleType: 'Fqdn'
          match: { host: 'httpbin.org' }
          action: { actionType: 'Allow' }
        }
        {
          name: 'deny-example'
          ruleType: 'Fqdn'
          match: { host: 'example.com' }
          action: { actionType: 'Deny' }
        }
        {
          name: 'deny-google'
          ruleType: 'Fqdn'
          match: { host: 'www.google.com' }
          action: { actionType: 'Deny' }
        }
      ]
    }
  }
  {
    // Audit mode with an allow-all rule — everything reachable, decisions logged.
    name: 'audit-allow'
    egressPolicy: {
      mode: 'Audit'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'allow-all'
          ruleType: 'Fqdn'
          match: { host: '*' }
          action: { actionType: 'Allow' }
        }
      ]
    }
  }
  {
    // Same rules as audit-vs-enforced-enforced but in Audit mode — the denied
    // host passes through here and is blocked by the Enforced twin below.
    name: 'audit-vs-enforced-audit'
    egressPolicy: {
      mode: 'Audit'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'deny-example'
          ruleType: 'Fqdn'
          match: { host: 'example.com' }
          action: { actionType: 'Deny' }
        }
        {
          name: 'allow-all'
          ruleType: 'Fqdn'
          match: { host: '*' }
          action: { actionType: 'Allow' }
        }
      ]
    }
  }
  {
    // Enforced twin of audit-vs-enforced-audit — the same deny rule blocks here.
    name: 'audit-vs-enforced-enforced'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'deny-example'
          ruleType: 'Fqdn'
          match: { host: 'example.com' }
          action: { actionType: 'Deny' }
        }
        {
          name: 'allow-all'
          ruleType: 'Fqdn'
          match: { host: '*' }
          action: { actionType: 'Allow' }
        }
      ]
    }
  }
]

// Managed-identity injection — only provisioned when a storage host is supplied,
// since it requires the account's managed identity to hold Storage Blob Data RBAC.
var managedIdentityPolicies = empty(managedIdentityStorageHost) ? [] : [
  {
    // Proxy injects a managed-identity bearer token for the storage host only;
    // httpbin.org is allowed without a token, proving host-scoped injection.
    name: 'managed-identity-injection'
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'mi-storage-token'
          ruleType: 'Fqdn'
          match: { host: managedIdentityStorageHost }
          action: {
            actionType: 'Transform'
            headers: [
              {
                name: 'Authorization'
                operation: 'Set'
                valueRef: {
                  managedIdentityRef: {
                    resource: 'https://storage.azure.com/.default'
                    format: 'Bearer {token}'
                  }
                }
              }
              { name: 'x-ms-version', value: '2023-11-03', operation: 'Set' }
            ]
          }
        }
        {
          name: 'allow-httpbin'
          ruleType: 'Fqdn'
          match: { host: 'httpbin.org' }
          action: { actionType: 'Allow' }
        }
      ]
    }
  }
]

var policies = concat(corePolicies, managedIdentityPolicies)

resource account 'Microsoft.CognitiveServices/accounts@2026-05-15-preview' existing = {
  name: accountName
}

// @batchSize(1) serializes creation: the account processes only one raiPolicies
// write at a time, so provisioning the catalog in parallel triggers RequestConflict.
@batchSize(1)
resource egressPolicies 'Microsoft.CognitiveServices/accounts/raiPolicies@2026-05-15-preview' = [
  for policy in policies: {
    parent: account
    name: policy.name
    properties: {
      basePolicyName: basePolicyName
      // The service rejects a policy whose contentFilters are null. Content
      // filtering is inherited from basePolicyName; an empty override array is
      // all that is required (the system default policy cannot be read back to
      // copy its filters, so we pass an empty array like the scenario helpers do).
      contentFilters: []
      egressPolicy: policy.egressPolicy
    }
  }
]

@description('Full ARM resource ID of the policy attached to the agent (agentPolicyName). Bound to the agent by azure.yaml via the RAI_POLICY_ID output.')
output RAI_POLICY_ID string = resourceId('Microsoft.CognitiveServices/accounts/raiPolicies', accountName, agentPolicyName)

@description('Names of every egress policy provisioned by this catalog.')
output policyNames array = [for policy in policies: policy.name]
