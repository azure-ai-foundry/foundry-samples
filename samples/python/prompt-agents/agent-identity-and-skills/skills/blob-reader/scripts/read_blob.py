#!/usr/bin/env python3
"""Download a blob from Azure Blob Storage using the caller's (agent's) managed identity.

Stdlib-only HTTP (urllib) so it runs on a bare Python image (harness sandbox) with no pip
installs. Authenticates to Storage with an AAD bearer token — no account key or SAS needed.
In a managed harness the *agent's* managed identity is used automatically via
DefaultAzureCredential; that identity needs **Storage Blob Data Reader** on the container
(or account).

Token resolution order (first that works wins):
  1. --token / env STORAGE_TOKEN or AZURE_STORAGE_TOKEN
  2. azure-identity DefaultAzureCredential (managed identity / sandbox identity / az login)
  3. `az account get-access-token` (Azure CLI on PATH)

Blob URL: --url or env HOLDINGS_BLOB_URL, e.g.
  https://<account>.blob.core.windows.net/<container>/<path/to/blob>

Examples:
  python read_blob.py --url https://acct.blob.core.windows.net/data/holdings.csv
  HOLDINGS_BLOB_URL=... python read_blob.py --out ./holdings.csv
"""
import argparse
import os
import subprocess
import sys
import urllib.error
import urllib.request

STORAGE_SCOPE = "https://storage.azure.com/.default"
X_MS_VERSION = "2021-08-06"


def _fail(msg, code=1):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(code)


def resolve_token(explicit=None):
    tok = explicit or os.environ.get("STORAGE_TOKEN") or os.environ.get("AZURE_STORAGE_TOKEN")
    if tok:
        return tok.strip()
    try:
        from azure.identity import DefaultAzureCredential  # type: ignore

        return DefaultAzureCredential().get_token(STORAGE_SCOPE).token
    except Exception:
        pass
    try:
        out = subprocess.check_output(
            ["az", "account", "get-access-token", "--scope", STORAGE_SCOPE,
             "--query", "accessToken", "-o", "tsv"],
            stderr=subprocess.DEVNULL, shell=(os.name == "nt"),
        )
        return out.decode().strip()
    except Exception:
        _fail("could not obtain a Storage token. Pass --token, set STORAGE_TOKEN, "
              "install azure-identity, or run 'az login'.")


def resolve_url(explicit=None):
    u = explicit or os.environ.get("HOLDINGS_BLOB_URL") or os.environ.get("BLOB_URL")
    if not u:
        _fail("no blob URL. Pass --url or set HOLDINGS_BLOB_URL "
              "(https://<account>.blob.core.windows.net/<container>/<blob>).")
    return u


def download(url, token):
    req = urllib.request.Request(
        url, method="GET",
        headers={"Authorization": f"Bearer {token}", "x-ms-version": X_MS_VERSION},
    )
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            return resp.read()
    except urllib.error.HTTPError as e:
        detail = e.read().decode(errors="replace")
        if e.code in (401, 403):
            _fail(f"HTTP {e.code}: identity lacks 'Storage Blob Data Reader' on this "
                  f"container/account. Grant the role and retry.\n{detail}")
        if e.code == 404:
            _fail(f"HTTP 404: blob not found. Check the URL.\n{detail}")
        _fail(f"HTTP {e.code} GET {url}\n{detail}")


def main():
    p = argparse.ArgumentParser(description="Azure Blob download via agent identity (AAD)")
    p.add_argument("--url", help="full blob URL (or env HOLDINGS_BLOB_URL)")
    p.add_argument("--token", help="bearer token (or env STORAGE_TOKEN)")
    p.add_argument("--out", help="output path (default: stream to stdout)")
    args = p.parse_args()

    url = resolve_url(args.url)
    token = resolve_token(args.token)
    data = download(url, token)

    if args.out:
        with open(args.out, "wb") as f:
            f.write(data)
        print(f"wrote {len(data)} bytes to {args.out}")
    else:
        sys.stdout.buffer.write(data)


if __name__ == "__main__":
    main()
