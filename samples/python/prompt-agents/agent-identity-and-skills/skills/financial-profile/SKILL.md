---
name: financial-profile
description: Fetch the user's financial profile (risk tolerance, investable cash, horizon, goals, current holdings, constraints) from the profile API while keeping the API credential secret. Use when an investment or planning agent needs the user's financial profile before producing analysis or a plan. Composes a Key Vault read (agent identity) with a profile-API call so the credential never enters the model context. Returns only the profile JSON.
---

# Financial Profile

## Overview

Return the user's financial profile as JSON, retrieved from a profile API (a Logic App)
that is protected by a shared-access signature (`sig`). The bundled
`scripts/get_profile.py` composes two steps **inside one process** so the `sig` never
appears in a prompt, tool argument, or the model's context:

1. Read the `sig` from **Azure Key Vault** using the **agent's managed identity**
   (`DefaultAzureCredential`).
2. GET the profile API with the `sig` appended and print **only** the profile JSON.

This is the "composed skill" pattern: contrast it with the generic
`keyvault-secret-reader` skill, which returns the raw secret (and therefore risks the value
entering context). Prefer this composed skill whenever the secret is only a means to an end.

## Prerequisites

- **`FINANCIAL_PROFILE_URL`** — the profile-API base invoke URL **without** the `&sig=...`
  part (the sig lives in Key Vault, not in config).
- **`KEYVAULT_URL`** — `https://<vault>.vault.azure.net`.
- **RBAC**: the agent identity must hold **Key Vault Secrets User** on the vault.
- Secret name defaults to `financial-profile-sig` (override with `--secret-name`).

## Usage

```bash
export FINANCIAL_PROFILE_URL="https://<logic-app-invoke-url-without-sig>"
export KEYVAULT_URL="https://<vault>.vault.azure.net"

python scripts/get_profile.py
```

Output (example):

```json
{
  "risk_tolerance": "moderate",
  "investable_cash_usd": 50000,
  "horizon_months": 6,
  "goals": ["retirement"],
  "current_holdings": [{ "ticker": "MSFT", "qty": 40 }],
  "constraints": { "no_crypto": true }
}
```

## Key rules

- **Never print or echo the `sig`.** The script redacts it even on error paths.
- **401/403 from Key Vault = missing role**, not transient — report and stop.
- The profile schema is fixed: `risk_tolerance`, `investable_cash_usd`, `horizon_months`,
  `goals`, `current_holdings`, `constraints`. Downstream planning should read these fields.
