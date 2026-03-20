import argparse
import importlib.util
import subprocess
import sys
from pathlib import Path
from shutil import which
from types import SimpleNamespace

import pytest


MODULE_PATH = Path(__file__).with_name("v1_to_v2_migration.py")
MIGRATION_DIR = MODULE_PATH.parent
POWERSHELL_WRAPPER = MIGRATION_DIR / "run-migration-docker-auth.ps1"
BASH_WRAPPER = MIGRATION_DIR / "run-migration-docker-auth.sh"
SPEC = importlib.util.spec_from_file_location("migration_v1_to_v2", MODULE_PATH)
MIGRATION = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MIGRATION
SPEC.loader.exec_module(MIGRATION)


def run_wrapper_command(command):
    return subprocess.run(
        command,
        cwd=MIGRATION_DIR,
        capture_output=True,
        text=True,
        timeout=30,
        check=False,
    )


def make_args(**overrides):
    base = {
        "assistant_id": None,
        "cosmos_endpoint": None,
        "use_api": False,
        "project_endpoint": None,
        "project_subscription": None,
        "project_resource_group": None,
        "project_name": None,
        "project_connection_string": None,
        "add_test_function": False,
        "add_test_mcp": False,
        "add_test_imagegen": False,
        "add_test_computer": False,
        "add_test_azurefunction": False,
        "production_resource": "nextgen-eastus",
        "production_subscription": "sub-id",
        "production_tenant": "tenant-id",
        "source_tenant": MIGRATION.SOURCE_TENANT_DEFAULT,
    }
    base.update(overrides)
    return argparse.Namespace(**base)


def test_parse_project_connection_string_supports_semicolon_form():
    result = MIGRATION.parse_project_connection_string(
        "eastus.api.azureml.ms;sub-id;rg-name;proj-name"
    )

    assert result == {
        "endpoint": "https://eastus.api.azureml.ms/api/projects/proj-name",
        "project_name": "proj-name",
        "subscription_id": "sub-id",
        "resource_group": "rg-name",
    }


def test_parse_project_connection_string_supports_key_value_form():
    result = MIGRATION.parse_project_connection_string(
        "endpoint=https://acct.services.ai.azure.com;projectname=my-project;subscriptionid=sub;resourcegroupname=rg"
    )

    assert result == {
        "endpoint": "https://acct.services.ai.azure.com/api/projects/my-project",
        "project_name": "my-project",
        "subscription_id": "sub",
        "resource_group": "rg",
    }


def test_normalize_agent_name_enforces_kebab_case():
    normalized = MIGRATION.normalize_agent_name("  Sales Copilot / EU West  ", "asst_123")

    assert normalized == "sales-copilot-eu-west"


def test_collect_supported_tools_skips_unsupported_and_embeds_resources():
    assistant = {
        "tools": [
            {"type": "connected_agent"},
            {"type": "event_binding"},
            {"type": "output_binding"},
            {"type": "file_search"},
            {"type": "code_interpreter"},
            {"type": "function", "function": {"name": "lookup"}},
            {"type": "mcp", "server_label": "svc", "server_url": "https://example"},
        ],
        "tool_resources": {
            "file_search": {"vector_store_ids": ["vs_123"]},
            "code_interpreter": {"file_ids": ["file_123"]},
        },
    }

    translated, warnings = MIGRATION.collect_supported_tools(assistant)

    assert [warning.tool_type for warning in warnings] == [
        "connected_agent",
        "event_binding",
        "output_binding",
    ]
    assert translated == [
        {"type": "file_search", "vector_store_ids": ["vs_123"]},
        {"type": "code_interpreter", "container": {"type": "auto", "file_ids": ["file_123"]}},
        {"type": "function", "function": {"name": "lookup"}},
        {"type": "mcp", "server_label": "svc", "server_url": "https://example"},
    ]


def test_build_prompt_agent_definition_appends_test_tools(monkeypatch):
    captured = {}

    class FakePromptAgentDefinition:
        def __init__(self, **kwargs):
            captured.update(kwargs)

    monkeypatch.setattr(MIGRATION, "PROJECTS_SDK_AVAILABLE", True)
    monkeypatch.setattr(MIGRATION, "PromptAgentDefinition", FakePromptAgentDefinition)

    assistant = {
        "id": "asst_001",
        "name": "Finance Analyst",
        "model": "gpt-4.1",
        "instructions": "Help with finance questions.",
        "tools": [{"type": "function", "function": {"name": "lookup_price"}}],
        "temperature": 0.2,
        "top_p": 0.7,
    }
    args = make_args(add_test_function=True, add_test_mcp=True)

    agent_name, definition, warnings, injected_count = MIGRATION.build_prompt_agent_definition(assistant, args)

    assert agent_name == "finance-analyst"
    assert isinstance(definition, FakePromptAgentDefinition)
    assert warnings == []
    assert injected_count == 2
    assert captured["model"] == "gpt-4.1"
    assert captured["instructions"] == "Help with finance questions."
    assert captured["temperature"] == 0.2
    assert captured["top_p"] == 0.7
    assert [tool["type"] for tool in captured["tools"]] == ["function", "function", "mcp"]


def test_validate_agent_runtime_uses_conversations_and_responses():
    calls = {"responses": [], "items": []}

    class FakeItemsApi:
        def create(self, conversation_id, items):
            calls["items"].append((conversation_id, items))

    class FakeConversationsApi:
        def __init__(self):
            self.items = FakeItemsApi()

        def create(self, items):
            calls["conversation_create"] = items
            return SimpleNamespace(id="conv_123")

    class FakeResponsesApi:
        def create(self, **kwargs):
            calls["responses"].append(kwargs)
            index = len(calls["responses"])
            return SimpleNamespace(output_text=f"response-{index}")

    openai_client = SimpleNamespace(
        conversations=FakeConversationsApi(),
        responses=FakeResponsesApi(),
    )
    agent = SimpleNamespace(name="finance-analyst", version="7")

    result = MIGRATION.validate_agent_runtime(openai_client, agent, injected_test_tool_count=1)

    assert result == {
        "conversation_id": "conv_123",
        "first_response": "response-1",
        "second_response": "response-2",
    }
    assert calls["conversation_create"][0]["type"] == "message"
    assert calls["items"] == [
        (
            "conv_123",
            [
                {
                    "type": "message",
                    "role": "user",
                    "content": "What was my previous request? Reply in one sentence so I can verify conversation memory.",
                }
            ],
        )
    ]
    assert len(calls["responses"]) == 2
    assert calls["responses"][0]["extra_body"]["agent_reference"] == {
        "name": "finance-analyst",
        "type": "agent_reference",
        "version": "7",
    }
    assert calls["responses"][0]["conversation"] == "conv_123"
    assert "use the most relevant one" in calls["responses"][0]["input"]


def test_migrate_single_assistant_retries_endpoint_and_returns_validation(monkeypatch):
    created_clients = []
    validation_calls = []

    class FakePromptAgentDefinition:
        def __init__(self, **kwargs):
            self.kwargs = kwargs

    class FakeAgentsApi:
        def __init__(self):
            self.calls = []

        def create_version(self, agent_name, definition):
            self.calls.append((agent_name, definition))
            return SimpleNamespace(name=agent_name, version="2", id=f"{agent_name}:2")

    class FakeProjectClient:
        def __init__(self, endpoint, credential):
            created_clients.append((endpoint, credential))
            self.endpoint = endpoint
            self.agents = FakeAgentsApi()
            if endpoint.startswith("https://bad"):
                raise RuntimeError("bad endpoint")

        def get_openai_client(self):
            return SimpleNamespace(close=lambda: None)

        def close(self):
            return None

    def fake_validate_agent_runtime(openai_client, agent, injected_test_tool_count):
        validation_calls.append((agent.name, agent.version, injected_test_tool_count))
        return {
            "conversation_id": "conv_1",
            "first_response": "ok-1",
            "second_response": "ok-2",
        }

    monkeypatch.setattr(MIGRATION, "PROJECTS_SDK_AVAILABLE", True)
    monkeypatch.setattr(MIGRATION, "PromptAgentDefinition", FakePromptAgentDefinition)
    monkeypatch.setattr(MIGRATION, "AIProjectClient", FakeProjectClient)
    monkeypatch.setattr(MIGRATION, "get_production_credential", lambda: "credential")
    monkeypatch.setattr(
        MIGRATION,
        "build_production_endpoint_candidates",
        lambda resource_name: ["https://bad/api/projects/x", "https://good/api/projects/x"],
    )
    monkeypatch.setattr(MIGRATION, "validate_agent_runtime", fake_validate_agent_runtime)

    assistant = {
        "id": "asst_001",
        "name": "Finance Analyst",
        "model": "gpt-4.1",
        "instructions": "Help with finance questions.",
        "tools": [{"type": "output_binding"}, {"type": "function", "function": {"name": "lookup"}}],
    }

    result = MIGRATION.migrate_single_assistant(assistant, make_args(add_test_function=True))

    assert [endpoint for endpoint, _credential in created_clients] == [
        "https://bad/api/projects/x",
        "https://good/api/projects/x",
    ]
    assert validation_calls == [("finance-analyst", "2", 1)]
    assert result == {
        "source_assistant_id": "asst_001",
        "source_assistant_name": "Finance Analyst",
        "agent_name": "finance-analyst",
        "agent_version": "2",
        "agent_id": "finance-analyst:2",
        "endpoint": "https://good/api/projects/x",
        "warning_count": 1,
        "validation": {
            "conversation_id": "conv_1",
            "first_response": "ok-1",
            "second_response": "ok-2",
        },
    }


def test_process_v1_assistants_to_v2_agents_aggregates_results(monkeypatch):
    args = make_args(use_api=True)
    source_assistants = [
        {"id": "asst_001", "name": "Agent One"},
        {"id": "asst_002", "name": "Agent Two"},
    ]
    migrated = []

    def fake_read_source_assistants(passed_args):
        assert passed_args is args
        return source_assistants

    def fake_migrate_single_assistant(assistant, passed_args):
        assert passed_args is args
        migrated.append(assistant["id"])
        return {
            "source_assistant_id": assistant["id"],
            "agent_name": assistant["name"].lower().replace(" ", "-"),
            "agent_version": "1",
        }

    monkeypatch.setattr(MIGRATION, "read_source_assistants", fake_read_source_assistants)
    monkeypatch.setattr(MIGRATION, "migrate_single_assistant", fake_migrate_single_assistant)

    results = MIGRATION.process_v1_assistants_to_v2_agents(args)

    assert migrated == ["asst_001", "asst_002"]
    assert results == [
        {
            "source_assistant_id": "asst_001",
            "agent_name": "agent-one",
            "agent_version": "1",
        },
        {
            "source_assistant_id": "asst_002",
            "agent_name": "agent-two",
            "agent_version": "1",
        },
    ]


def test_process_v1_assistants_to_v2_agents_raises_when_no_sources(monkeypatch):
    monkeypatch.setattr(MIGRATION, "read_source_assistants", lambda args: [])

    with pytest.raises(RuntimeError, match="No source assistants were found"):
        MIGRATION.process_v1_assistants_to_v2_agents(make_args())


def test_powershell_wrapper_help_returns_usage_text():
    pwsh = which("pwsh")
    if not pwsh:
        pytest.skip("pwsh is not available")

    result = run_wrapper_command(
        [pwsh, "-NoProfile", "-NonInteractive", "-File", str(POWERSHELL_WRAPPER), "--help"]
    )

    assert result.returncode == 0
    combined_output = result.stdout + result.stderr
    assert "Usage:" in combined_output
    assert "--production-resource" in combined_output


def test_bash_wrapper_parses_and_returns_usage_text():
    bash = which("bash")
    if not bash:
        pytest.skip("bash is not available")

    syntax_result = run_wrapper_command([bash, "-n", BASH_WRAPPER.name])
    assert syntax_result.returncode == 0, syntax_result.stderr or syntax_result.stdout

    help_result = run_wrapper_command([bash, BASH_WRAPPER.name, "--help"])
    assert help_result.returncode == 0
    combined_output = help_result.stdout + help_result.stderr
    assert "Usage:" in combined_output
    assert "--production-resource" in combined_output