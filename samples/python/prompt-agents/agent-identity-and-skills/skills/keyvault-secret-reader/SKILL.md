---
name: keyvault-secret-reader
description: Read a named secret from Azure Key Vault using the caller's (agent's) managed identity, without ever placing the secret value in a prompt or tool argument. Use when an agent needs a credential, API key, connection string, or `sig` held in Key Vault — including from inside a managed harness or sandbox where DefaultAzureCredential resolves to the agent identity. Requires the identity to hold the 'Key Vault Secrets User' role on the vault.
---

# Key Vault Secret Reader

## Overview

Fetch a secret from Azure Key Vault via the **caller's identity**. The bundled
`scripts/read_secret.py` is stdlib-only (no pip installs) so it runs on a bare Python
image such as a harness sandbox. In a managed harness the **agent's managed identity** is
used automatically via `DefaultAzureCredential`.

**Secret hygiene:** the secret value stays inside the script process. By default the CLI
**masks** the value; only pass `--show` when a downstream step must consume the raw value,
and pipe it directly rather than printing it into the conversation.

## Prerequisites

- **`KEYVAULT_URL`** — `https://<vault>.vault.azure.net` (or `--vault`).
- **A token** — resolved automatically: `--token` / `KEYVAULT_TOKEN` env, then
  `DefaultAzureCredential` (managed identity / sandbox identity / `az login`), then
  `az account get-access-token`. Scope is `https://vault.azure.net/.default`.
- **RBAC**: the identity must have **Key Vault Secrets User** on the vault
  (role id `4633458b-17de-408a-b874-0445c86b69e6`). A 401/403 means the role is missing.

## Usage

```bash
export KEYVAULT_URL="https://<vault>.vault.azure.net"

# Inspect a secret without revealing it (masked value + length):
python scripts/read_secret.py get financial-profile-sig

# Emit the raw value for a downstream step (avoid printing to the user):
SIG="$(python scripts/read_secret.py get financial-profile-sig --show)"
```

## Key rules

- **Never echo the raw value to the user.** Use the masked form to confirm retrieval; use
  `--show` only to hand the value to another program.
- **401/403 = missing role**, not a transient error — report it plainly and stop; do not retry.
- Pass `--version` to pin a specific secret version; latest is used by default.
