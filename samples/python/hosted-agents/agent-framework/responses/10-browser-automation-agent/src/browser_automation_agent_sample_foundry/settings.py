from __future__ import annotations

import os
from dataclasses import dataclass

DEFAULT_AZURE_SCOPE = "https://ai.azure.com/.default"


@dataclass(frozen=True)
class AgentSettings:
    project_endpoint: str
    model: str
    azure_scope: str
    toolbox_name: str
    mcp_timeout_seconds: int
    playwright_cli_timeout_seconds: int
    profile: str
    prompt_file: str | None
    verbose: bool


@dataclass(frozen=True)
class ScopedAzureCredential:
    credential: object
    scope: str

    def get_token(self, *scopes: str, **kwargs: object) -> object:
        return self.credential.get_token(self.scope, **kwargs)


def is_template_placeholder(value: str) -> bool:
    stripped = value.strip()
    return stripped.startswith("{{") and stripped.endswith("}}")


def require_env(*names: str) -> str:
    for name in names:
        value = os.getenv(name)
        if value and not is_template_placeholder(value):
            return value
    raise RuntimeError(f"Missing required environment variable: {' or '.join(names)}")


def optional_env(*names: str) -> str | None:
    for name in names:
        value = os.getenv(name)
        if value and not is_template_placeholder(value):
            return value
    return None


def int_env(name: str, default_value: int) -> int:
    value = os.getenv(name)
    if not value or is_template_placeholder(value):
        return default_value
    return int(value)


def bool_env(name: str) -> bool:
    return os.getenv(name, "").lower() in {"1", "true", "yes"}


def normalize_foundry_project_endpoint(endpoint: str) -> str:
    normalized = endpoint.rstrip("/")
    suffix = "/openai/v1"
    if normalized.endswith(suffix):
        return normalized[: -len(suffix)]
    return normalized


def make_settings() -> AgentSettings:
    project_endpoint = normalize_foundry_project_endpoint(
        require_env("FOUNDRY_PROJECT_ENDPOINT", "AZURE_FOUNDRY_PROJECT_ENDPOINT", "AZURE_AI_PROJECT_ENDPOINT"),
    )

    return AgentSettings(
        project_endpoint=project_endpoint,
        model=require_env("AZURE_AI_MODEL_DEPLOYMENT_NAME", "BROWSER_AGENT_MODEL", "AZURE_OPENAI_DEPLOYMENT_NAME"),
        azure_scope=optional_env("AZURE_AI_SCOPE", "AZURE_OPENAI_SCOPE") or DEFAULT_AZURE_SCOPE,
        toolbox_name=require_env("BROWSER_AGENT_TOOLBOX_NAME", "TOOLBOX_NAME"),
        mcp_timeout_seconds=int_env("BROWSER_AGENT_MCP_TIMEOUT_SECONDS", 120),
        playwright_cli_timeout_seconds=int_env("BROWSER_AGENT_PLAYWRIGHT_CLI_TIMEOUT_SECONDS", 180),
        profile=optional_env("BROWSER_AGENT_PROFILE") or "general",
        prompt_file=optional_env("BROWSER_AGENT_PROMPT_FILE"),
        verbose=bool_env("BROWSER_AGENT_VERBOSE"),
    )

