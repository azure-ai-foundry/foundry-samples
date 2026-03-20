from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from dataclasses import dataclass
from typing import Any, Dict, Iterable, List, Optional, Tuple

import requests

try:
    from azure.ai.projects import AIProjectClient
    from azure.ai.projects.models import PromptAgentDefinition
    from azure.core.credentials import AccessToken
    from azure.identity import DefaultAzureCredential

    PROJECTS_SDK_AVAILABLE = True
except ImportError:
    AIProjectClient = None
    PromptAgentDefinition = None
    AccessToken = None
    DefaultAzureCredential = None
    PROJECTS_SDK_AVAILABLE = False

try:
    from azure.cosmos import CosmosClient

    COSMOS_AVAILABLE = True
except ImportError:
    CosmosClient = None
    COSMOS_AVAILABLE = False


SOURCE_TENANT_DEFAULT = "72f988bf-86f1-41af-91ab-2d7cd011db47"
LEGACY_API_VERSION = os.getenv("AGENTS_API_VERSION") or os.getenv("ASSISTANT_API_VERSION") or "2025-05-15-preview"
LEGACY_API_HOST = os.getenv("AGENTS_HOST") or "eastus.api.azureml.ms"
LEGACY_SUBSCRIPTION_ID = os.getenv("AGENTS_SUBSCRIPTION") or "921496dc-987f-410f-bd57-426eb2611356"
LEGACY_RESOURCE_GROUP = os.getenv("AGENTS_RESOURCE_GROUP") or "agents-e2e-tests-eastus"
LEGACY_WORKSPACE = os.getenv("AGENTS_WORKSPACE") or "basicaccountjqqa@e2e-tests@AML"

COSMOS_CONNECTION_STRING = os.getenv("COSMOS_CONNECTION_STRING") or os.getenv("COSMOS_DB_CONNECTION_STRING")
COSMOS_DATABASE = os.getenv("COSMOS_DB_DATABASE_NAME") or "testDB2"
COSMOS_CONTAINER = os.getenv("COSMOS_DB_CONTAINER_NAME") or "testContainer1"


@dataclass
class MigrationWarning:
    tool_type: str
    message: str
    recommendation: Optional[str] = None


class StaticTokenCredential:
    def __init__(self, token: str):
        self._token = token

    def get_token(self, *scopes: str, **kwargs: Any) -> Any:
        return AccessToken(self._token, 2**31 - 1)


def require_projects_sdk() -> None:
    if not PROJECTS_SDK_AVAILABLE:
        raise RuntimeError(
            "azure-ai-projects is required. Install with: pip install \"azure-ai-projects>=2.0.0\" azure-identity"
        )


def get_token_from_az(tenant_id: Optional[str] = None) -> str:
    command = [
        "az",
        "account",
        "get-access-token",
        "--scope",
        "https://ai.azure.com/.default",
        "--query",
        "accessToken",
        "-o",
        "tsv",
    ]
    if tenant_id:
        command.extend(["--tenant", tenant_id])

    result = subprocess.run(command, capture_output=True, text=True, check=False)
    if result.returncode != 0:
        message = result.stderr.strip() or result.stdout.strip() or "az account get-access-token failed"
        raise RuntimeError(message)

    token = result.stdout.strip()
    if not token:
        raise RuntimeError("Azure CLI returned an empty access token")
    return token


def get_source_token(source_tenant: Optional[str]) -> str:
    env_token = os.getenv("AZ_TOKEN")
    if env_token:
        return env_token
    return get_token_from_az(source_tenant or SOURCE_TENANT_DEFAULT)


def get_production_credential() -> Any:
    require_projects_sdk()
    env_token = os.getenv("PRODUCTION_TOKEN")
    if env_token:
        return StaticTokenCredential(env_token)
    return DefaultAzureCredential()


def request_json(method: str, url: str, token: str, **kwargs: Any) -> Any:
    headers = kwargs.pop("headers", {})
    headers["Authorization"] = f"Bearer {token}"
    headers["Accept"] = "application/json"
    response = requests.request(method, url, headers=headers, timeout=60, **kwargs)
    response.raise_for_status()
    if not response.content:
        return None
    return response.json()


def legacy_api_base() -> str:
    return (
        f"https://{LEGACY_API_HOST}/agents/v1.0/subscriptions/{LEGACY_SUBSCRIPTION_ID}"
        f"/resourceGroups/{LEGACY_RESOURCE_GROUP}/providers/Microsoft.MachineLearningServices/workspaces/{LEGACY_WORKSPACE}"
    )


def parse_project_connection_string(connection_string: str) -> Dict[str, str]:
    raw_parts = [part.strip() for part in connection_string.split(";") if part.strip()]
    key_value_parts = [part for part in raw_parts if "=" in part]

    if key_value_parts:
        parsed: Dict[str, str] = {}
        for part in key_value_parts:
            key, value = part.split("=", 1)
            parsed[key.strip().lower()] = value.strip()
        endpoint = parsed.get("endpoint")
        project_name = parsed.get("projectname") or parsed.get("project")
        if not endpoint or not project_name:
            raise ValueError("Connection string must include endpoint and projectname values")
        if endpoint.startswith("https://") and "/api/projects/" in endpoint:
            project_endpoint = endpoint.rstrip("/")
        else:
            host = endpoint.replace("https://", "").strip("/")
            project_endpoint = f"https://{host}/api/projects/{project_name}"
        return {
            "endpoint": project_endpoint,
            "project_name": project_name,
            "subscription_id": parsed.get("subscriptionid", ""),
            "resource_group": parsed.get("resourcegroupname", ""),
        }

    if len(raw_parts) != 4:
        raise ValueError(
            "Connection string must be either key=value pairs or host;subscription-id;resource-group;project-name"
        )

    host, subscription_id, resource_group, project_name = raw_parts
    if host.startswith("https://") and "/api/projects/" in host:
        endpoint = host.rstrip("/")
    else:
        endpoint = f"https://{host.strip('/')}/api/projects/{project_name}"
    return {
        "endpoint": endpoint,
        "project_name": project_name,
        "subscription_id": subscription_id,
        "resource_group": resource_group,
    }


def normalize_agent_name(raw_name: Optional[str], assistant_id: str) -> str:
    candidate = (raw_name or assistant_id or "migrated-agent").strip().lower()
    candidate = re.sub(r"[^a-z0-9-]+", "-", candidate)
    candidate = re.sub(r"-{2,}", "-", candidate).strip("-")
    if not candidate:
        candidate = f"agent-{assistant_id.lower()}"
        candidate = re.sub(r"[^a-z0-9-]+", "-", candidate)
    if not candidate[0].isalnum():
        candidate = f"agent-{candidate}"
    return candidate[:64].rstrip("-")


def build_production_endpoint_candidates(resource_name: str) -> List[str]:
    explicit_endpoint = os.getenv("PRODUCTION_PROJECT_ENDPOINT")
    if explicit_endpoint:
        return [explicit_endpoint.rstrip("/")]

    project_name = os.getenv("PRODUCTION_PROJECT_NAME") or resource_name

    if resource_name.startswith("https://") and "/api/projects/" in resource_name:
        return [resource_name.rstrip("/")]

    if ".services.ai.azure.com" in resource_name:
        host = resource_name.replace("https://", "").strip("/")
        return [f"https://{host}/api/projects/{project_name}"]

    hosts = [
        f"{resource_name}.services.ai.azure.com",
        f"{resource_name}-resource.services.ai.azure.com",
    ]
    candidates: List[str] = []
    for host in hosts:
        endpoint = f"https://{host}/api/projects/{project_name}"
        if endpoint not in candidates:
            candidates.append(endpoint)
    return candidates


def list_assistants_from_legacy_api(token: str) -> List[Dict[str, Any]]:
    url = f"{legacy_api_base()}/assistants"
    payload = request_json("GET", url, token, params={"api-version": LEGACY_API_VERSION, "limit": "100"})
    if isinstance(payload, dict):
        for key in ("data", "assistants", "items"):
            value = payload.get(key)
            if isinstance(value, list):
                return value
    if isinstance(payload, list):
        return payload
    return []


def get_assistant_from_legacy_api(assistant_id: str, token: str) -> Dict[str, Any]:
    url = f"{legacy_api_base()}/assistants/{assistant_id}"
    return request_json(
        "GET",
        url,
        token,
        params={"api-version": LEGACY_API_VERSION, "include[]": "internal_metadata"},
    )


def list_assistants_from_project_endpoint(project_endpoint: str, token: str) -> List[Dict[str, Any]]:
    url = f"{project_endpoint.rstrip('/')}/assistants"
    payload = request_json("GET", url, token, params={"api-version": LEGACY_API_VERSION, "limit": "100"})
    if isinstance(payload, dict):
        for key in ("data", "assistants", "items"):
            value = payload.get(key)
            if isinstance(value, list):
                return value
    if isinstance(payload, list):
        return payload
    return []


def get_assistant_from_project_endpoint(project_endpoint: str, assistant_id: str, token: str) -> Dict[str, Any]:
    url = f"{project_endpoint.rstrip('/')}/assistants/{assistant_id}"
    return request_json("GET", url, token, params={"api-version": LEGACY_API_VERSION})


def extract_assistant_from_cosmos_document(document: Dict[str, Any]) -> Optional[Dict[str, Any]]:
    if isinstance(document.get("data"), dict):
        return document["data"]

    if isinstance(document.get("object"), dict):
        object_payload = document["object"]
        if object_payload.get("object_type") == "v1_assistant":
            return object_payload.get("data") if isinstance(object_payload.get("data"), dict) else object_payload

    return None


def list_assistants_from_cosmos(connection_string: str, assistant_id: Optional[str]) -> List[Dict[str, Any]]:
    if not COSMOS_AVAILABLE:
        raise RuntimeError("azure-cosmos is required for legacy Cosmos DB input")

    client = CosmosClient.from_connection_string(connection_string)
    container = client.get_database_client(COSMOS_DATABASE).get_container_client(COSMOS_CONTAINER)

    documents = list(container.query_items(query="SELECT * FROM c", enable_cross_partition_query=True))
    assistants: List[Dict[str, Any]] = []
    for document in documents:
        assistant = extract_assistant_from_cosmos_document(document)
        if not assistant:
            continue
        if assistant_id and assistant.get("id") != assistant_id:
            continue
        assistants.append(assistant)
    return assistants


def read_source_assistants(args: argparse.Namespace) -> List[Dict[str, Any]]:
    source_token = get_source_token(args.source_tenant)

    if args.project_connection_string:
        connection = parse_project_connection_string(args.project_connection_string)
        print(f"Reading source assistants from project connection string via {connection['endpoint']}")
        if args.assistant_id:
            return [get_assistant_from_project_endpoint(connection["endpoint"], args.assistant_id, source_token)]
        return list_assistants_from_project_endpoint(connection["endpoint"], source_token)

    if args.project_endpoint:
        print(f"Reading source assistants from project endpoint {args.project_endpoint}")
        if args.assistant_id:
            return [get_assistant_from_project_endpoint(args.project_endpoint, args.assistant_id, source_token)]
        return list_assistants_from_project_endpoint(args.project_endpoint, source_token)

    if args.use_api:
        print("Reading source assistants from legacy Assistants API")
        if args.assistant_id:
            return [get_assistant_from_legacy_api(args.assistant_id, source_token)]
        return list_assistants_from_legacy_api(source_token)

    print(f"Reading source assistants from Cosmos DB {COSMOS_DATABASE}/{COSMOS_CONTAINER}")
    connection_string = args.cosmos_endpoint or COSMOS_CONNECTION_STRING
    if not connection_string:
        raise RuntimeError("COSMOS_CONNECTION_STRING or COSMOS_DB_CONNECTION_STRING is required for Cosmos DB input")
    return list_assistants_from_cosmos(connection_string, args.assistant_id)


def parse_json_if_needed(value: Any) -> Any:
    if isinstance(value, str):
        try:
            return json.loads(value)
        except json.JSONDecodeError:
            return value
    return value


def collect_supported_tools(v1_assistant: Dict[str, Any]) -> Tuple[List[Dict[str, Any]], List[MigrationWarning]]:
    unsupported: List[MigrationWarning] = []
    tools = parse_json_if_needed(v1_assistant.get("tools") or [])
    tool_resources = parse_json_if_needed(v1_assistant.get("tool_resources") or {})

    if not isinstance(tools, list):
        tools = []
    if not isinstance(tool_resources, dict):
        tool_resources = {}

    translated: List[Dict[str, Any]] = []
    for raw_tool in tools:
        tool = parse_json_if_needed(raw_tool)
        if not isinstance(tool, dict):
            continue

        tool_type = tool.get("type")
        if tool_type == "connected_agent":
            unsupported.append(
                MigrationWarning(
                    tool_type=tool_type,
                    message="Connected agents are not supported in the new Agent Service.",
                    recommendation="Use workflows or the A2A tool for multi-agent orchestration.",
                )
            )
            continue
        if tool_type == "event_binding":
            unsupported.append(
                MigrationWarning(
                    tool_type=tool_type,
                    message="event_binding is not supported in the new Agent Service.",
                )
            )
            continue
        if tool_type == "output_binding":
            unsupported.append(
                MigrationWarning(
                    tool_type=tool_type,
                    message="output_binding is not supported in the new Agent Service.",
                    recommendation="Use capture_structured_outputs for structured output capture.",
                )
            )
            continue

        migrated = {"type": tool_type}

        if tool_type == "file_search":
            file_search = tool_resources.get("file_search", {})
            vector_store_ids = file_search.get("vector_store_ids")
            if vector_store_ids:
                migrated["vector_store_ids"] = vector_store_ids
        elif tool_type == "code_interpreter":
            code_resources = tool_resources.get("code_interpreter", {})
            migrated["container"] = {"type": "auto"}
            if code_resources.get("file_ids"):
                migrated["container"]["file_ids"] = code_resources["file_ids"]
        elif tool_type == "function" and isinstance(tool.get("function"), dict):
            migrated["function"] = tool["function"]
        else:
            for key, value in tool.items():
                if key == "type" or value is None:
                    continue
                migrated[key] = value

        translated.append(migrated)

    return translated, unsupported


def build_test_tool_definitions(args: argparse.Namespace) -> List[Dict[str, Any]]:
    tools: List[Dict[str, Any]] = []

    if args.add_test_function:
        tools.append(
            {
                "type": "function",
                "function": {
                    "name": "get_current_temperature",
                    "description": "Return a fake current temperature for a requested location.",
                    "parameters": {
                        "type": "object",
                        "properties": {
                            "location": {"type": "string", "description": "City and state or country."},
                            "unit": {
                                "type": "string",
                                "enum": ["Celsius", "Fahrenheit"],
                                "description": "Requested temperature unit.",
                            },
                        },
                        "required": ["location", "unit"],
                    },
                },
            }
        )

    if args.add_test_mcp:
        tools.append(
            {
                "type": "mcp",
                "server_label": "dmcp",
                "server_description": "A dice-rolling MCP test server.",
                "server_url": "https://dmcp-server.deno.dev/sse",
                "require_approval": "never",
            }
        )

    if args.add_test_computer:
        tools.append(
            {
                "type": "computer_use_preview",
                "display_width": 1024,
                "display_height": 768,
                "environment": "browser",
            }
        )

    if args.add_test_imagegen:
        tools.append({"type": "image_generation"})

    if args.add_test_azurefunction:
        tools.append(
            {
                "type": "azure_function",
                "name": "foo",
                "description": "A migration validation Azure Function tool.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "query": {"type": "string", "description": "The question to ask."},
                    },
                    "required": ["query"],
                },
                "input_queue": {
                    "queue_name": "azure-function-foo-input",
                    "storage_service_endpoint": "https://127.0.0.1:8001",
                },
                "output_queue": {
                    "queue_name": "azure-function-foo-output",
                    "storage_service_endpoint": "https://127.0.0.1:8001",
                },
            }
        )

    return tools


def build_prompt_agent_definition(v1_assistant: Dict[str, Any], args: argparse.Namespace) -> Tuple[str, Any, List[MigrationWarning], int]:
    require_projects_sdk()
    assistant_id = str(v1_assistant.get("id") or "unknown")
    agent_name = normalize_agent_name(v1_assistant.get("name"), assistant_id)

    migrated_tools, warnings = collect_supported_tools(v1_assistant)
    injected_test_tools = build_test_tool_definitions(args)
    migrated_tools.extend(injected_test_tools)

    definition_kwargs: Dict[str, Any] = {
        "model": v1_assistant.get("model"),
        "instructions": v1_assistant.get("instructions") or "You are a helpful assistant.",
        "tools": migrated_tools,
    }
    if isinstance(v1_assistant.get("temperature"), (int, float)):
        definition_kwargs["temperature"] = v1_assistant["temperature"]
    if isinstance(v1_assistant.get("top_p"), (int, float)):
        definition_kwargs["top_p"] = v1_assistant["top_p"]

    definition = PromptAgentDefinition(**definition_kwargs)
    return agent_name, definition, warnings, len(injected_test_tools)


def extract_response_text(response: Any) -> str:
    output_text = getattr(response, "output_text", None)
    if output_text:
        return output_text

    output = getattr(response, "output", None) or []
    collected: List[str] = []
    for item in output:
        if getattr(item, "type", None) != "message":
            continue
        for block in getattr(item, "content", []) or []:
            text = getattr(block, "text", None)
            if text:
                collected.append(text)
    return "\n".join(collected).strip()


def build_validation_prompts(injected_test_tool_count: int) -> Tuple[Dict[str, Any], str, Dict[str, Any], str]:
    first_conversation_item = {
        "type": "message",
        "role": "user",
        "content": (
            "You are being validated after a migration from Assistants to Foundry Agent Service. "
            "Briefly state that you are active and summarize what capabilities you expose."
        ),
    }
    first_response_input = (
        "Answer the user and, if you have tools available, use the most relevant one before replying. "
        "Mention any tool you used."
        if injected_test_tool_count
        else "Answer the user in one short paragraph."
    )

    follow_up_item = {
        "type": "message",
        "role": "user",
        "content": "What was my previous request? Reply in one sentence so I can verify conversation memory.",
    }
    second_response_input = "Answer the latest question and explicitly rely on the existing conversation context."

    return first_conversation_item, first_response_input, follow_up_item, second_response_input


def validate_agent_runtime(openai_client: Any, agent: Any, injected_test_tool_count: int) -> Dict[str, str]:
    first_item, first_input, follow_up_item, second_input = build_validation_prompts(injected_test_tool_count)

    conversation = openai_client.conversations.create(items=[first_item])
    agent_reference: Dict[str, Any] = {
        "agent_reference": {
            "name": agent.name,
            "type": "agent_reference",
        }
    }
    if getattr(agent, "version", None):
        agent_reference["agent_reference"]["version"] = agent.version

    first_response = openai_client.responses.create(
        conversation=conversation.id,
        input=first_input,
        extra_body=agent_reference,
    )

    openai_client.conversations.items.create(
        conversation_id=conversation.id,
        items=[follow_up_item],
    )

    second_response = openai_client.responses.create(
        conversation=conversation.id,
        input=second_input,
        extra_body=agent_reference,
    )

    return {
        "conversation_id": conversation.id,
        "first_response": extract_response_text(first_response),
        "second_response": extract_response_text(second_response),
    }


def format_warnings(warnings: Iterable[MigrationWarning]) -> None:
    warning_list = list(warnings)
    if not warning_list:
        return

    print("  Unsupported classic tools skipped during migration:")
    for warning in warning_list:
        print(f"    - {warning.tool_type}: {warning.message}")
        if warning.recommendation:
            print(f"      Recommendation: {warning.recommendation}")


def migrate_single_assistant(v1_assistant: Dict[str, Any], args: argparse.Namespace) -> Dict[str, Any]:
    require_projects_sdk()

    agent_name, definition, warnings, injected_test_tool_count = build_prompt_agent_definition(v1_assistant, args)
    endpoint_candidates = build_production_endpoint_candidates(args.production_resource)
    last_error: Optional[Exception] = None

    for endpoint in endpoint_candidates:
        project_client = None
        openai_client = None
        try:
            print(f"Creating or versioning agent '{agent_name}' at {endpoint}")
            project_client = AIProjectClient(endpoint=endpoint, credential=get_production_credential())
            openai_client = project_client.get_openai_client()

            agent = project_client.agents.create_version(agent_name=agent_name, definition=definition)
            validation = validate_agent_runtime(openai_client, agent, injected_test_tool_count)
            format_warnings(warnings)
            return {
                "source_assistant_id": v1_assistant.get("id"),
                "source_assistant_name": v1_assistant.get("name"),
                "agent_name": agent.name,
                "agent_version": getattr(agent, "version", None),
                "agent_id": getattr(agent, "id", None),
                "endpoint": endpoint,
                "warning_count": len(warnings),
                "validation": validation,
            }
        except Exception as exc:
            last_error = exc
            print(f"  Failed against {endpoint}: {exc}")
        finally:
            if openai_client and hasattr(openai_client, "close"):
                try:
                    openai_client.close()
                except Exception:
                    pass
            if project_client and hasattr(project_client, "close"):
                try:
                    project_client.close()
                except Exception:
                    pass

    if last_error is None:
        raise RuntimeError("No production endpoint candidates were available")
    raise last_error


def process_v1_assistants_to_v2_agents(args: argparse.Namespace) -> List[Dict[str, Any]]:
    assistants = read_source_assistants(args)
    if not assistants:
        raise RuntimeError("No source assistants were found")

    print(f"Found {len(assistants)} source assistants to migrate")
    results: List[Dict[str, Any]] = []

    for index, assistant in enumerate(assistants, start=1):
        print("-" * 72)
        print(f"[{index}/{len(assistants)}] Migrating assistant {assistant.get('id', 'unknown')}")
        results.append(migrate_single_assistant(assistant, args))

    return results


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Migrate classic Assistants or classic Foundry agents to versioned Foundry Agent Service prompt agents",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "Examples:\n"
            "  python v1_to_v2_migration.py --use-api --production-resource nextgen-eastus "
            "--production-subscription <subscription-id> --production-tenant <tenant-id> asst_abc123\n\n"
            "  python v1_to_v2_migration.py --project-endpoint https://your-project.services.ai.azure.com/api/projects/your-project "
            "--production-resource nextgen-eastus --production-subscription <subscription-id> --production-tenant <tenant-id>\n\n"
            "  python v1_to_v2_migration.py --project-connection-string \"eastus.api.azureml.ms;subscription-id;resource-group;project-name\" "
            "--production-resource nextgen-eastus --production-subscription <subscription-id> --production-tenant <tenant-id>\n"
        ),
    )

    parser.add_argument(
        "assistant_id",
        nargs="?",
        default=None,
        help="Optional source assistant ID. If omitted, migrates every assistant returned by the selected input source.",
    )
    parser.add_argument(
        "cosmos_endpoint",
        nargs="?",
        default=None,
        help="Optional Cosmos DB connection string. If omitted, uses COSMOS_CONNECTION_STRING or COSMOS_DB_CONNECTION_STRING.",
    )
    parser.add_argument("--use-api", action="store_true", help="Read source assistants from the legacy Assistants API.")
    parser.add_argument("--project-endpoint", type=str, help="Read source assistants from a Foundry project endpoint.")
    parser.add_argument(
        "--project-subscription",
        type=str,
        help="Retained for CLI compatibility. Not required by the rewritten migration engine.",
    )
    parser.add_argument(
        "--project-resource-group",
        type=str,
        help="Retained for CLI compatibility. Not required by the rewritten migration engine.",
    )
    parser.add_argument(
        "--project-name",
        type=str,
        help="Retained for CLI compatibility. Not required by the rewritten migration engine.",
    )
    parser.add_argument(
        "--project-connection-string",
        type=str,
        help="Read source assistants from a project connection string. Beta SDK support is optional; the tool can also parse the endpoint directly.",
    )

    parser.add_argument("--add-test-function", action="store_true", help="Inject a function-calling test tool after migration.")
    parser.add_argument("--add-test-mcp", action="store_true", help="Inject an MCP test tool after migration.")
    parser.add_argument("--add-test-imagegen", action="store_true", help="Inject an image generation test tool after migration.")
    parser.add_argument("--add-test-computer", action="store_true", help="Inject a computer-use test tool after migration.")
    parser.add_argument("--add-test-azurefunction", action="store_true", help="Inject an Azure Function test tool after migration.")

    parser.add_argument(
        "--production-resource",
        type=str,
        required=True,
        help="Production Foundry resource name or full production project endpoint.",
    )
    parser.add_argument(
        "--production-subscription",
        type=str,
        required=True,
        help="Required for CLI compatibility and deployment context logging.",
    )
    parser.add_argument(
        "--production-tenant",
        type=str,
        required=True,
        help="Production tenant ID used by the surrounding auth scripts.",
    )
    parser.add_argument(
        "--source-tenant",
        type=str,
        default=SOURCE_TENANT_DEFAULT,
        help="Source tenant used when fetching source assistant definitions through Azure CLI tokens.",
    )

    return parser


def main() -> None:
    parser = build_parser()
    args = parser.parse_args()

    if args.project_endpoint and args.project_connection_string:
        parser.error("Choose either --project-endpoint or --project-connection-string, not both")

    print("Starting Foundry Agent Service migration")
    print(f"Production resource: {args.production_resource}")
    print(f"Production subscription: {args.production_subscription}")
    print(f"Production tenant: {args.production_tenant}")
    print(f"Source tenant: {args.source_tenant}")
    if args.project_connection_string and PROJECTS_SDK_AVAILABLE and hasattr(AIProjectClient, "from_connection_string"):
        print("Connection string beta support detected in installed SDK")
    elif args.project_connection_string:
        print("Connection string beta helper not present in installed SDK; falling back to direct endpoint parsing")

    try:
        results = process_v1_assistants_to_v2_agents(args)
    except Exception as exc:
        print(f"Migration failed: {exc}")
        sys.exit(1)

    print("=" * 72)
    print("Migration complete")
    for result in results:
        print(
            f"- {result['source_assistant_id']} -> {result['agent_name']}:{result['agent_version']} "
            f"at {result['endpoint']}"
        )
        print(f"  Conversation: {result['validation']['conversation_id']}")
        print(f"  First response: {result['validation']['first_response']}")
        print(f"  Second response: {result['validation']['second_response']}")


if __name__ == "__main__":
    main()
