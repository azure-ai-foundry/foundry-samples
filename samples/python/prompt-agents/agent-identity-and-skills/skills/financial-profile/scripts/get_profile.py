#!/usr/bin/env python3
"""Fetch the user's financial profile from the profile API, keeping the credential secret.

Composes two steps entirely inside this process, so the credential never enters a prompt,
tool argument, or the model's context:
  1. Read the profile-API shared-access signature (`sig`) from Azure Key Vault using the
     caller's (agent's) managed identity.
  2. GET the profile API (Logic App) with that `sig` appended, and print ONLY the returned
     profile JSON.

Stdlib-only HTTP (urllib) so it runs on a bare Python image (harness sandbox), no pip installs.

Inputs (env or flags):
  FINANCIAL_PROFILE_URL  base Logic App invoke URL WITHOUT the `&sig=...` query part
  KEYVAULT_URL           https://<vault>.vault.azure.net
  --secret-name          Key Vault secret holding the sig (default: financial-profile-sig)

Token resolution (Key Vault): DefaultAzureCredential (agent identity) -> az CLI fallback.

Example:
  export FINANCIAL_PROFILE_URL="https://prod-19.northcentralus.logic.azure.com:443/workflows/.../invoke?api-version=2016-10-01&sp=...&sv=1.0"
  export KEYVAULT_URL="https://myvault.vault.azure.net"
  python get_profile.py
"""
import argparse
import json
import os
import subprocess
import sys
import urllib.error
import urllib.request

KV_API_VERSION = "7.4"
KV_SCOPE = "https://vault.azure.net/.default"


def _fail(msg, code=1):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(code)


def _kv_token():
    tok = os.environ.get("KEYVAULT_TOKEN")
    if tok:
        return tok.strip()
    try:
        from azure.identity import DefaultAzureCredential  # type: ignore

        return DefaultAzureCredential().get_token(KV_SCOPE).token
    except Exception:
        pass
    try:
        out = subprocess.check_output(
            ["az", "account", "get-access-token", "--scope", KV_SCOPE,
             "--query", "accessToken", "-o", "tsv"],
            stderr=subprocess.DEVNULL, shell=(os.name == "nt"),
        )
        return out.decode().strip()
    except Exception:
        _fail("could not obtain a Key Vault token (agent identity / az login required).")


def read_sig(vault, secret_name):
    url = f"{vault.rstrip('/')}/secrets/{secret_name}?api-version={KV_API_VERSION}"
    req = urllib.request.Request(url, method="GET",
                                 headers={"Authorization": f"Bearer {_kv_token()}"})
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return json.loads(resp.read().decode() or "{}").get("value", "")
    except urllib.error.HTTPError as e:
        detail = e.read().decode(errors="replace")
        if e.code in (401, 403):
            _fail(f"HTTP {e.code}: agent identity lacks 'Key Vault Secrets User' on {vault}.\n{detail}")
        _fail(f"HTTP {e.code} reading secret '{secret_name}'\n{detail}")


def fetch_profile(base_url, sig):
    sep = "&" if "?" in base_url else "?"
    url = f"{base_url}{sep}sig={sig}"
    req = urllib.request.Request(url, method="GET")
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return resp.read().decode()
    except urllib.error.HTTPError as e:
        detail = e.read().decode(errors="replace")
        # Never echo the sig on failure.
        _fail(f"HTTP {e.code} calling profile API (sig redacted)\n{detail}")


def main():
    p = argparse.ArgumentParser(description="Fetch financial profile (credential stays secret)")
    p.add_argument("--url", help="profile API base URL without sig (or env FINANCIAL_PROFILE_URL)")
    p.add_argument("--vault", help="Key Vault URL (or env KEYVAULT_URL)")
    p.add_argument("--secret-name", default="financial-profile-sig",
                   help="Key Vault secret holding the sig (default: financial-profile-sig)")
    args = p.parse_args()

    base_url = args.url or os.environ.get("FINANCIAL_PROFILE_URL")
    if not base_url:
        _fail("no profile URL. Pass --url or set FINANCIAL_PROFILE_URL (without &sig=).")
    vault = args.vault or os.environ.get("KEYVAULT_URL")
    if not vault:
        _fail("no vault. Pass --vault or set KEYVAULT_URL.")

    sig = read_sig(vault, args.secret_name)
    if not sig:
        _fail(f"secret '{args.secret_name}' was empty.")
    body = fetch_profile(base_url, sig)

    # Print only the profile JSON (pretty-printed if it parses).
    try:
        print(json.dumps(json.loads(body), indent=2))
    except Exception:
        print(body)


if __name__ == "__main__":
    main()
