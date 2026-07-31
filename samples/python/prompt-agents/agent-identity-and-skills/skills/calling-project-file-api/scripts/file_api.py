#!/usr/bin/env python3
"""CLI for the Azure AI Foundry project Files API (list / upload / download / delete / get).

Stdlib-only HTTP (urllib) so it runs on a bare Python image with no pip installs.
Token resolution order (first that works wins):
  1. --token / env PROJECT_API_TOKEN or AZURE_AI_TOKEN
  2. azure-identity DefaultAzureCredential (if the package is installed)
  3. `az account get-access-token` (if the Azure CLI is on PATH)

Endpoint resolution: --endpoint or env PROJECT_ENDPOINT, e.g.
  https://<resource>.services.ai.azure.com/api/projects/<project>

Examples:
  python file_api.py list
  python file_api.py upload ./data.jsonl --purpose assistants
  python file_api.py get   assistant-abc123
  python file_api.py download assistant-abc123 --out ./data.jsonl
  python file_api.py delete assistant-abc123
"""
import argparse
import json
import mimetypes
import os
import subprocess
import sys
import urllib.error
import urllib.request
import uuid

DEFAULT_API_VERSION = "2025-05-15-preview"
TOKEN_SCOPE = "https://ai.azure.com/.default"


def _fail(msg, code=1):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(code)


def resolve_token(explicit=None):
    tok = explicit or os.environ.get("PROJECT_API_TOKEN") or os.environ.get("AZURE_AI_TOKEN")
    if tok:
        return tok.strip()
    # DefaultAzureCredential (works in Hand / managed identity / az login)
    try:
        from azure.identity import DefaultAzureCredential  # type: ignore

        return DefaultAzureCredential().get_token(TOKEN_SCOPE).token
    except Exception:
        pass
    # az CLI fallback
    try:
        out = subprocess.check_output(
            ["az", "account", "get-access-token", "--scope", TOKEN_SCOPE,
             "--query", "accessToken", "-o", "tsv"],
            stderr=subprocess.DEVNULL, shell=(os.name == "nt"),
        )
        return out.decode().strip()
    except Exception:
        _fail("could not obtain a token. Pass --token, or set PROJECT_API_TOKEN, "
              "or install azure-identity, or run 'az login'.")


def resolve_endpoint(explicit=None):
    ep = explicit or os.environ.get("PROJECT_ENDPOINT")
    if not ep:
        _fail("no project endpoint. Pass --endpoint or set PROJECT_ENDPOINT "
              "(e.g. https://<res>.services.ai.azure.com/api/projects/<proj>).")
    return ep.rstrip("/")


def _url(endpoint, path, api_version):
    sep = "&" if "?" in path else "?"
    return f"{endpoint}/files{path}{sep}api-version={api_version}"


def _request(method, url, token, data=None, headers=None, raw=False):
    hdrs = {"Authorization": f"Bearer {token}"}
    if headers:
        hdrs.update(headers)
    req = urllib.request.Request(url, data=data, method=method, headers=hdrs)
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            body = resp.read()
            return body if raw else json.loads(body.decode() or "{}")
    except urllib.error.HTTPError as e:
        detail = e.read().decode(errors="replace")
        _fail(f"HTTP {e.code} {method} {url}\n{detail}")


def _multipart(fields, file_field, filename, file_bytes):
    boundary = f"----fileapi{uuid.uuid4().hex}"
    crlf = b"\r\n"
    buf = []
    for name, value in fields.items():
        buf += [f"--{boundary}".encode(), crlf,
                f'Content-Disposition: form-data; name="{name}"'.encode(), crlf, crlf,
                str(value).encode(), crlf]
    ctype = mimetypes.guess_type(filename)[0] or "application/octet-stream"
    buf += [f"--{boundary}".encode(), crlf,
            f'Content-Disposition: form-data; name="{file_field}"; filename="{filename}"'.encode(),
            crlf, f"Content-Type: {ctype}".encode(), crlf, crlf,
            file_bytes, crlf, f"--{boundary}--".encode(), crlf]
    return b"".join(buf), f"multipart/form-data; boundary={boundary}"


def cmd_list(args, endpoint, token):
    print(json.dumps(_request("GET", _url(endpoint, "", args.api_version), token), indent=2))


def cmd_get(args, endpoint, token):
    print(json.dumps(_request("GET", _url(endpoint, f"/{args.file_id}", args.api_version), token), indent=2))


def cmd_upload(args, endpoint, token):
    path = args.path
    if not os.path.isfile(path):
        _fail(f"file not found: {path}")
    with open(path, "rb") as f:
        file_bytes = f.read()
    body, ctype = _multipart({"purpose": args.purpose}, "file",
                             os.path.basename(path), file_bytes)
    res = _request("POST", _url(endpoint, "", args.api_version), token,
                   data=body, headers={"Content-Type": ctype})
    print(json.dumps(res, indent=2))


def cmd_download(args, endpoint, token):
    raw = _request("GET", _url(endpoint, f"/{args.file_id}/content", args.api_version),
                   token, raw=True)
    if args.out:
        with open(args.out, "wb") as f:
            f.write(raw)
        print(f"wrote {len(raw)} bytes to {args.out}")
    else:
        sys.stdout.buffer.write(raw)


def cmd_delete(args, endpoint, token):
    print(json.dumps(_request("DELETE", _url(endpoint, f"/{args.file_id}", args.api_version), token), indent=2))


def main():
    p = argparse.ArgumentParser(description="Azure AI Foundry project Files API CLI")
    p.add_argument("--endpoint", help="project endpoint (or env PROJECT_ENDPOINT)")
    p.add_argument("--token", help="bearer token (or env PROJECT_API_TOKEN)")
    p.add_argument("--api-version", default=DEFAULT_API_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    sub.add_parser("list").set_defaults(func=cmd_list)

    g = sub.add_parser("get")
    g.add_argument("file_id")
    g.set_defaults(func=cmd_get)

    u = sub.add_parser("upload")
    u.add_argument("path")
    u.add_argument("--purpose", default="assistants",
                   help="assistants | batch | fine-tune | vision (default: assistants)")
    u.set_defaults(func=cmd_upload)

    d = sub.add_parser("download")
    d.add_argument("file_id")
    d.add_argument("--out", help="output path (default: stdout)")
    d.set_defaults(func=cmd_download)

    x = sub.add_parser("delete")
    x.add_argument("file_id")
    x.set_defaults(func=cmd_delete)

    args = p.parse_args()
    endpoint = resolve_endpoint(args.endpoint)
    token = resolve_token(args.token)
    args.func(args, endpoint, token)


if __name__ == "__main__":
    main()
