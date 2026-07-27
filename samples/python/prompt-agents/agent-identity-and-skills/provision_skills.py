#!/usr/bin/env python3
"""Provision this sample's toolbox, connection, and skills on a Foundry project.

Run this ONCE (and again whenever you edit a skill) before running the notebook or
`main.py`. It fully owns the sample's tool surface — you do NOT pre-create a toolbox or a
connection. In order:

  1. Publish skills : multipart POST {project}/skills/{name}/versions?api-version=v1
                      (each file part's filename is its path relative to the skill folder,
                      e.g. `scripts/get_profile.py`; description auto-extracts from SKILL.md)
  2. Create/attach  : POST {project}/toolboxes/{TOOLBOX_NAME}/versions?api-version=v1 with a
                      `skills:[{type:"skill_reference", name}]` list (this CREATES the toolbox
                      if it does not exist), then PATCH {default_version: N}.
  3. Connection     : ARM PUT {PROJECT_RESOURCE_ID}/connections/{TOOLBOX_CONNECTION_NAME}
                      (category `RemoteTool`, `authType` `AgenticIdentityToken` — the agent's
                      own identity token, `target`
                      the toolbox MCP endpoint). The prompt agent references the toolbox only
                      through this connection; its id is derived, so there is no env var to set.

Data-plane Skills/Toolbox calls require the header `Foundry-Features: Skills=V1Preview` and a
token for https://ai.azure.com/.default. The connection PUT is an ARM control-plane call made
via `az rest` (management.azure.com).

Env (or a local .env):
  AZURE_AI_PROJECT_ENDPOINT   https://<res>.services.ai.azure.com/api/projects/<project>
  PROJECT_RESOURCE_ID         ARM id of the Foundry project (for the connection PUT)

Identity needs the `Azure AI User` role on the project (author-time).
"""
import json
import mimetypes
import os
import sys
import tempfile
import urllib.error
import urllib.request
import uuid
from pathlib import Path

import dotenv
from azure.identity import DefaultAzureCredential

from sample_config import (
    TOOLBOX_CONNECTION_NAME,
    TOOLBOX_NAME,
    run_az,
    toolbox_connection_id,
    toolbox_mcp_url,
)

SKILLS_DIR = Path(__file__).parent / "skills"
API_VERSION = "v1"
TOKEN_SCOPE = "https://ai.azure.com/.default"
SKILLS_FEATURE_HEADER = {"Foundry-Features": "Skills=V1Preview"}
# ARM api-version for the CognitiveServices project connection sub-resource.
ARM_CONNECTION_API_VERSION = "2025-10-01-preview"


def _fail(msg, code=1):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(code)


def _token():
    return DefaultAzureCredential().get_token(TOKEN_SCOPE).token


def _request(method, url, token, data=None, headers=None, allow_404=False):
    hdrs = {"Authorization": f"Bearer {token}"}
    hdrs.update(SKILLS_FEATURE_HEADER)
    if headers:
        hdrs.update(headers)
    req = urllib.request.Request(url, data=data, method=method, headers=hdrs)
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            body = resp.read().decode() or "{}"
            return json.loads(body) if body.strip().startswith(("{", "[")) else body
    except urllib.error.HTTPError as e:
        if allow_404 and e.code == 404:
            return None
        _fail(f"HTTP {e.code} {method} {url}\n{e.read().decode(errors='replace')}")


def _multipart_from_folder(folder: Path):
    """Build a multipart body whose parts are every file under `folder`.

    Each part's filename is the file's path relative to `folder` (POSIX-style), which is how
    the Skills API reconstructs the skill's directory layout (e.g. scripts/get_profile.py).
    """
    boundary = f"----skill{uuid.uuid4().hex}"
    crlf = b"\r\n"
    buf = []
    for path in sorted(p for p in folder.rglob("*") if p.is_file()):
        rel = path.relative_to(folder).as_posix()
        ctype = mimetypes.guess_type(rel)[0] or "application/octet-stream"
        buf += [
            f"--{boundary}".encode(), crlf,
            f'Content-Disposition: form-data; name="files"; filename="{rel}"'.encode(), crlf,
            f"Content-Type: {ctype}".encode(), crlf, crlf,
            path.read_bytes(), crlf,
        ]
    buf += [f"--{boundary}--".encode(), crlf]
    return b"".join(buf), f"multipart/form-data; boundary={boundary}"


def publish_skill(endpoint, token, folder: Path):
    name = folder.name
    body, ctype = _multipart_from_folder(folder)
    url = f"{endpoint}/skills/{name}/versions?api-version={API_VERSION}"
    res = _request("POST", url, token, data=body, headers={"Content-Type": ctype})
    ver = res.get("version") if isinstance(res, dict) else "?"
    print(f"  published skill '{name}' (version {ver})")
    return name


def attach_to_toolbox(endpoint, token, toolbox, skill_names):
    # GET is 404-tolerant: on the first run the toolbox does not exist yet, and the POST
    # below creates it. On later runs we preserve any existing tools on the new version.
    tb = _request("GET", f"{endpoint}/toolboxes/{toolbox}?api-version={API_VERSION}", token,
                  allow_404=True)
    tools = (tb or {}).get("tools", []) if isinstance(tb, dict) else []
    payload = {
        "tools": tools,
        "skills": [{"type": "skill_reference", "name": n} for n in skill_names],
    }
    created = _request(
        "POST", f"{endpoint}/toolboxes/{toolbox}/versions?api-version={API_VERSION}", token,
        data=json.dumps(payload).encode(), headers={"Content-Type": "application/json"},
    )
    new_ver = created.get("version") if isinstance(created, dict) else None
    if new_ver is None:
        _fail(f"toolbox version create returned no version: {created}")
    _request(
        "PATCH", f"{endpoint}/toolboxes/{toolbox}?api-version={API_VERSION}", token,
        data=json.dumps({"default_version": new_ver}).encode(),
        headers={"Content-Type": "application/json"},
    )
    verb = "created toolbox and attached" if tb is None else "attached"
    print(f"  {verb} {len(skill_names)} skill(s) to toolbox '{toolbox}' "
          f"(version {new_ver}, now default)")


def create_connection(project_resource_id, target):
    """Idempotently create the RemoteTool connection fronting the toolbox MCP endpoint.

    Uses ARM (`az rest`, control-plane) to create a connection with the `AgenticIdentityToken`
    auth type: because the toolbox lives in the same project, the running agent's own identity
    token (its `Azure AI User` role) is what authorizes the MCP call — no key or SAS is stored
    on the connection.
    """
    conn_id = toolbox_connection_id(project_resource_id)
    body = {
        "properties": {
            "authType": "AgenticIdentityToken",
            "category": "RemoteTool",
            "target": target,
            "isSharedToAll": False,
        }
    }
    # Pass the JSON via a temp file (--body @file) so Windows shell quoting can't mangle it.
    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8") as fh:
        json.dump(body, fh)
        body_path = fh.name
    try:
        r = run_az(
            ["rest", "--method", "PUT",
             "--url", f"https://management.azure.com{conn_id}?api-version={ARM_CONNECTION_API_VERSION}",
             "--headers", "Content-Type=application/json",
             "--body", f"@{body_path}"],
        )
    finally:
        os.unlink(body_path)
    if r.returncode != 0:
        _fail(f"connection PUT failed:\n{r.stderr or r.stdout}")
    print(f"  connection '{TOOLBOX_CONNECTION_NAME}' -> {target}")
    return conn_id


def main():
    dotenv.load_dotenv()
    endpoint = os.environ.get("AZURE_AI_PROJECT_ENDPOINT")
    if not endpoint:
        _fail("set AZURE_AI_PROJECT_ENDPOINT (https://<res>.services.ai.azure.com/api/projects/<project>)")
    endpoint = endpoint.rstrip("/")
    project_resource_id = os.environ.get("PROJECT_RESOURCE_ID")
    if not project_resource_id:
        _fail("set PROJECT_RESOURCE_ID (ARM id of the Foundry project) to create the connection")

    folders = sorted(p for p in SKILLS_DIR.iterdir() if p.is_dir() and (p / "SKILL.md").is_file())
    if not folders:
        _fail(f"no skills with a SKILL.md found under {SKILLS_DIR}")

    token = _token()
    print(f"Publishing {len(folders)} skill(s) to {endpoint} ...")
    names = [publish_skill(endpoint, token, f) for f in folders]

    print(f"Attaching to toolbox '{TOOLBOX_NAME}' ...")
    attach_to_toolbox(endpoint, token, TOOLBOX_NAME, names)

    print("Creating the toolbox connection (AgenticIdentityToken) ...")
    conn_id = create_connection(project_resource_id, toolbox_mcp_url(endpoint))

    print(f"Done. The agent references the toolbox via this connection (no env var needed):\n"
          f"  {conn_id}")


if __name__ == "__main__":
    main()
