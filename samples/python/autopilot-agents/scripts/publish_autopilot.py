"""Publish a deployed Foundry agent as a Microsoft 365 Autopilot."""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Any

API_VERSION = "2025-11-15-preview"


@dataclass(frozen=True)
class PublicationSettings:
    display_name: str
    short_description: str
    full_description: str
    developer_name: str
    developer_website_url: str
    privacy_url: str
    terms_of_use_url: str
    can_respond_without_mention: bool


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Publish the single deployed agent in an azd environment as a "
            "tenant-scoped Microsoft 365 Autopilot."
        )
    )
    parser.add_argument("--display-name", required=True)
    parser.add_argument("--short-description", required=True)
    parser.add_argument("--full-description", required=True)
    parser.add_argument("--developer-name", default="Microsoft")
    parser.add_argument(
        "--developer-website-url",
        default="https://www.microsoft.com",
    )
    parser.add_argument(
        "--privacy-url",
        default="https://privacy.microsoft.com",
    )
    parser.add_argument(
        "--terms-of-use-url",
        default="https://www.microsoft.com/legal/terms-of-use",
    )
    parser.add_argument(
        "--can-respond-without-mention",
        action=argparse.BooleanOptionalAction,
        default=True,
    )
    parser.add_argument(
        "--app-version",
        help=(
            "Microsoft 365 app version. By default, the deployed numeric agent "
            "version is published as 1.0.<agent-version>."
        ),
    )
    parser.add_argument(
        "--project-dir",
        type=Path,
        default=Path.cwd(),
        help="Sample directory containing azure.yaml (default: current directory).",
    )
    return parser.parse_args()


def run_json(command: list[str], cwd: Path | None = None) -> Any:
    result = subprocess.run(
        command,
        check=True,
        capture_output=True,
        text=True,
        cwd=cwd,
    )
    return json.loads(result.stdout)


def azd_environment(project_dir: Path) -> dict[str, str]:
    values = run_json(
        ["azd", "env", "get-values", "--output", "json"],
        cwd=project_dir,
    )
    return {str(key): str(value) for key, value in values.items()}


def agent_details(environment: dict[str, str]) -> tuple[str, str]:
    deployed_agents = sorted(
        (
            environment[name_key],
            environment[version_key],
        )
        for name_key in environment
        if name_key.startswith("AGENT_")
        and name_key.endswith("_NAME")
        and (version_key := f"{name_key[:-5]}_VERSION") in environment
    )
    if len(deployed_agents) != 1:
        raise RuntimeError(
            "Expected exactly one deployed agent with matching AGENT_*_NAME and "
            f"AGENT_*_VERSION values; found {len(deployed_agents)}."
        )

    return deployed_agents[0]


def default_app_version(agent_version: str) -> str:
    if not agent_version.isdigit():
        raise RuntimeError(
            "The deployed agent version is not numeric. Pass --app-version "
            "explicitly before publishing."
        )
    return f"1.0.{agent_version}"


def azure_cli_executable() -> str:
    executable = shutil.which("az")
    if not executable:
        raise RuntimeError(
            "Azure CLI was not found on PATH. Install it and restart the terminal."
        )
    return executable


def access_token(tenant_id: str | None) -> str:
    command = [
        azure_cli_executable(),
        "account",
        "get-access-token",
        "--resource",
        "https://ai.azure.com",
        "--query",
        "accessToken",
        "--output",
        "tsv",
    ]
    if tenant_id:
        command.extend(["--tenant", tenant_id])

    result = subprocess.run(
        command,
        check=True,
        capture_output=True,
        text=True,
    )
    token = result.stdout.strip()
    if not token:
        raise RuntimeError("Azure CLI returned an empty access token.")
    return token


def request_json(
    method: str,
    url: str,
    token: str,
    body: dict[str, Any] | None = None,
) -> dict[str, Any]:
    data = json.dumps(body).encode("utf-8") if body is not None else None
    request = urllib.request.Request(
        url,
        data=data,
        method=method,
        headers={
            "Accept": "application/json",
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json",
        },
    )

    try:
        with urllib.request.urlopen(request) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        details = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(
            f"Microsoft Foundry request failed ({error.code}): {details}"
        ) from error


def publication_body(
    blueprint_client_id: str,
    version: str,
    settings: PublicationSettings,
) -> dict[str, Any]:
    return {
        "agentDisplayName": settings.display_name,
        "publishAsAutopilot": True,
        "publishScope": "Tenant",
        "appVersion": version,
        "canRespondWithoutMention": settings.can_respond_without_mention,
        "shortDescription": settings.short_description,
        "fullDescription": settings.full_description,
        "developerName": settings.developer_name,
        "developerWebsiteUrl": settings.developer_website_url,
        "privacyUrl": settings.privacy_url,
        "termsOfUseUrl": settings.terms_of_use_url,
        "useAgenticUserTemplate": True,
        "agenticUserTemplate": {
            "Id": "digitalWorkerTemplate",
            "File": "agenticUserTemplateManifest.json",
            "SchemaVersion": "0.1.0-preview",
            "AgentIdentityBlueprintId": blueprint_client_id,
            "CommunicationProtocol": "activityProtocol",
        },
    }


def main() -> None:
    args = parse_args()
    project_dir = args.project_dir.resolve()
    if not (project_dir / "azure.yaml").is_file():
        raise RuntimeError(f"azure.yaml was not found in {project_dir}.")

    settings = PublicationSettings(
        display_name=args.display_name,
        short_description=args.short_description,
        full_description=args.full_description,
        developer_name=args.developer_name,
        developer_website_url=args.developer_website_url,
        privacy_url=args.privacy_url,
        terms_of_use_url=args.terms_of_use_url,
        can_respond_without_mention=args.can_respond_without_mention,
    )
    environment = azd_environment(project_dir)
    project_endpoint = environment.get(
        "AZURE_AI_PROJECT_ENDPOINT"
    ) or environment.get("FOUNDRY_PROJECT_ENDPOINT")
    if not project_endpoint:
        raise RuntimeError("Missing AZURE_AI_PROJECT_ENDPOINT after deployment.")

    agent_name, agent_version = agent_details(environment)
    token = access_token(environment.get("AZURE_TENANT_ID"))

    version_url = (
        f"{project_endpoint.rstrip('/')}/agents/{agent_name}/versions/"
        f"{agent_version}?api-version={API_VERSION}"
    )
    deployed_agent = request_json("GET", version_url, token)
    blueprint_client_id = deployed_agent.get("blueprint", {}).get("client_id")
    if not blueprint_client_id:
        raise RuntimeError("The deployed agent did not return a blueprint client ID.")

    publish_url = (
        f"{project_endpoint.rstrip('/')}/agents/{agent_name}/microsoft365/publish"
        f"?api-version={API_VERSION}"
    )
    version = args.app_version or default_app_version(agent_version)

    try:
        response = request_json(
            "POST",
            publish_url,
            token,
            publication_body(blueprint_client_id, version, settings),
        )
    except RuntimeError as error:
        if "version already exists" in str(error).casefold():
            print(f"Autopilot publication {version} already exists; nothing to do.")
            return
        raise

    print(f"Published {settings.display_name} version {version}.")
    if response:
        print(json.dumps(response, indent=2))
    print(
        "A Microsoft 365 administrator must now approve the agent blueprint at "
        "https://admin.cloud.microsoft/?#/agents/all/requested"
    )


if __name__ == "__main__":
    main()
