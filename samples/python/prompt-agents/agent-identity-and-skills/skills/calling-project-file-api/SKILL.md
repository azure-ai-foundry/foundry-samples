---
name: calling-project-file-api
description: List, upload, download, get, and delete files in an Azure AI Foundry project via its data-plane Files API. Use when an agent needs to manage project files (list files, upload a file, download file content, get file metadata, delete a file) against a project endpoint such as an ai.azure.com services projects URL, including from inside a managed harness or sandbox.
---

# Calling Project File API

## Overview

Perform file operations against an Azure AI Foundry project's data-plane **Files API**
(`{PROJECT_ENDPOINT}/files`). Use the bundled `scripts/file_api.py` — it is stdlib-only
(no pip installs), so it runs on a bare Python image such as a harness sandbox.

## Prerequisites

Provide two things (via env vars or CLI flags):

- **`PROJECT_ENDPOINT`** — `https://<resource>.services.ai.azure.com/api/projects/<project>`
- **A token** — resolved automatically in this order:
  1. `--token` flag or `PROJECT_API_TOKEN` / `AZURE_AI_TOKEN` env var
  2. `azure-identity` `DefaultAzureCredential` (managed identity / sandbox identity / `az login`)
  3. `az account get-access-token` CLI fallback

  Token scope is `https://ai.azure.com/.default`. In a managed harness the sandbox
  identity is used automatically via `DefaultAzureCredential` — no token needs to be passed.

## Usage

```bash
export PROJECT_ENDPOINT="https://<resource>.services.ai.azure.com/api/projects/<project>"

python scripts/file_api.py list
python scripts/file_api.py upload ./notes.txt --purpose assistants   # -> returns { "id": "assistant-...", ... }
python scripts/file_api.py get      assistant-abc123
python scripts/file_api.py download assistant-abc123 --out ./notes.txt   # omit --out to stream to stdout
python scripts/file_api.py delete   assistant-abc123
```

All commands print the JSON response (download writes bytes). Non-2xx responses print
`ERROR: HTTP <code> ...` with the server body and exit non-zero.

## Key rules (enforced by the API)

- **`purpose` is required on upload**: `assistants` (default) | `batch` | `fine-tune` | `vision`.
- **File extension allow-list** — uploads with an unsupported extension fail with HTTP 400.
  Notably **`.jsonl` is rejected**; rename batch/fine-tune data to `.json` or `.txt` first.
  Allowed: `c cpp css csv doc docx gif go html java jpeg jpg js json md pdf php pkl png pptx py rb tar tex ts txt webp xlsx xml zip`.
- **api-version** is mandatory and defaults to `2025-05-15-preview` (override with `--api-version`).
- If `list` returns **401/403**, the caller identity lacks a data-plane role on the project —
  report that plainly instead of retrying.

## Reference

For endpoint details, response shapes, raw `curl` equivalents, and the full list of
gotchas learned from live testing, see [references/files-api.md](references/files-api.md).
