---
name: blob-reader
description: Download a blob (CSV, JSON, or any file) from Azure Blob Storage using the caller's (agent's) managed identity via AAD — no account key or SAS. Use when an agent needs to read input data a user placed in their own blob container, including from inside a managed harness or sandbox where DefaultAzureCredential resolves to the agent identity. Requires the identity to hold the 'Storage Blob Data Reader' role on the container or account.
---

# Blob Reader

## Overview

Download a blob from Azure Blob Storage authenticated with an **AAD bearer token** (the
caller's identity), not an account key or SAS. The bundled `scripts/read_blob.py` is
stdlib-only (no pip installs) so it runs on a bare Python image such as a harness sandbox.
In a managed harness the **agent's managed identity** is used automatically via
`DefaultAzureCredential`.

## Prerequisites

- **`HOLDINGS_BLOB_URL`** (or `--url`) — the full blob URL,
  `https://<account>.blob.core.windows.net/<container>/<blob>`.
- **A token** — resolved automatically: `--token` / `STORAGE_TOKEN` env, then
  `DefaultAzureCredential`, then `az account get-access-token`. Scope is
  `https://storage.azure.com/.default`.
- **RBAC**: the identity must have **Storage Blob Data Reader** on the container or account
  (role id `2a2b9908-6ea1-4ae2-8e65-a410df84e7d1`). A 401/403 means the role is missing.

## Usage

```bash
export HOLDINGS_BLOB_URL="https://<account>.blob.core.windows.net/<container>/holdings.csv"

python scripts/read_blob.py --out ./holdings.csv   # or omit --out to stream to stdout
```

## Key rules

- **401/403 = missing role**, not transient — report it plainly and stop; do not retry.
- **404 = wrong URL** (container/blob path). The URL is the container + blob path, not a SAS.
- Reads only; this skill never writes or deletes. Data-plane role, not control-plane.
