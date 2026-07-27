#!/usr/bin/env python3
"""Read a secret from Azure Key Vault using the caller's identity.

Stdlib-only HTTP (urllib) so it runs on a bare Python image (e.g. a harness sandbox)
with no pip installs. In a managed harness, the *agent's* managed identity is used
automatically via DefaultAzureCredential -- no token needs to be passed, and the secret
value is never placed in a prompt or tool argument; it stays inside this process.

Token resolution order (first that works wins):
  1. --token / env KEYVAULT_TOKEN or AZURE_KEYVAULT_TOKEN
  2. azure-identity DefaultAzureCredential (managed identity / sandbox identity / az login)
  3. `az account get-access-token` (Azure CLI on PATH)

Vault resolution: --vault or env KEYVAULT_URL, e.g. https://<vault>.vault.azure.net

Examples:
  python read_secret.py get financial-profile-sig
  python read_secret.py get financial-profile-sig --vault https://myvault.vault.azure.net
  python read_secret.py get my-secret --show   # print the value (default masks it)
"""
import argparse
import json
import os
import subprocess
import sys
import urllib.error
import urllib.request

DEFAULT_API_VERSION = "7.4"
TOKEN_SCOPE = "https://vault.azure.net/.default"


def _fail(msg, code=1):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(code)


def resolve_token(explicit=None):
    tok = explicit or os.environ.get("KEYVAULT_TOKEN") or os.environ.get("AZURE_KEYVAULT_TOKEN")
    if tok:
        return tok.strip()
    try:
        from azure.identity import DefaultAzureCredential  # type: ignore

        return DefaultAzureCredential().get_token(TOKEN_SCOPE).token
    except Exception:
        pass
    try:
        out = subprocess.check_output(
            ["az", "account", "get-access-token", "--scope", TOKEN_SCOPE,
             "--query", "accessToken", "-o", "tsv"],
            stderr=subprocess.DEVNULL, shell=(os.name == "nt"),
        )
        return out.decode().strip()
    except Exception:
        _fail("could not obtain a Key Vault token. Pass --token, set KEYVAULT_TOKEN, "
              "install azure-identity, or run 'az login'.")


def resolve_vault(explicit=None):
    v = explicit or os.environ.get("KEYVAULT_URL")
    if not v:
        _fail("no vault. Pass --vault or set KEYVAULT_URL "
              "(e.g. https://<vault>.vault.azure.net).")
    return v.rstrip("/")


def get_secret(vault, name, token, api_version=DEFAULT_API_VERSION, version=""):
    path = f"/secrets/{name}" + (f"/{version}" if version else "")
    url = f"{vault}{path}?api-version={api_version}"
    req = urllib.request.Request(url, method="GET",
                                 headers={"Authorization": f"Bearer {token}"})
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return json.loads(resp.read().decode() or "{}")
    except urllib.error.HTTPError as e:
        detail = e.read().decode(errors="replace")
        if e.code in (401, 403):
            _fail(f"HTTP {e.code}: identity lacks 'Key Vault Secrets User' on {vault}. "
                  f"Grant the role and retry.\n{detail}")
        _fail(f"HTTP {e.code} GET {url}\n{detail}")


def cmd_get(args, vault, token):
    res = get_secret(vault, args.name, token, args.api_version, args.version)
    value = res.get("value", "")
    if args.show:
        # Print only the raw value so callers can capture it without JSON noise.
        sys.stdout.write(value)
    else:
        masked = (value[:2] + "***" + value[-2:]) if len(value) > 4 else "***"
        print(json.dumps({"id": res.get("id"), "name": args.name,
                          "value_masked": masked, "length": len(value)}, indent=2))


def main():
    p = argparse.ArgumentParser(description="Azure Key Vault secret reader (agent identity)")
    p.add_argument("--vault", help="vault URL (or env KEYVAULT_URL)")
    p.add_argument("--token", help="bearer token (or env KEYVAULT_TOKEN)")
    p.add_argument("--api-version", default=DEFAULT_API_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    g = sub.add_parser("get")
    g.add_argument("name")
    g.add_argument("--version", default="", help="specific secret version (default: latest)")
    g.add_argument("--show", action="store_true",
                   help="print the raw secret value (default masks it)")
    g.set_defaults(func=cmd_get)

    args = p.parse_args()
    vault = resolve_vault(args.vault)
    token = resolve_token(args.token)
    args.func(args, vault, token)


if __name__ == "__main__":
    main()
