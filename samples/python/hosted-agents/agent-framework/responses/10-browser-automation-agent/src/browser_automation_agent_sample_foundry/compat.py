from __future__ import annotations

import importlib
import importlib.metadata

import agent_framework as _agent_framework


def ensure_agent_framework_compat() -> None:
    """Patch preview hosting packages that expect root agent_framework exports.

    The package versions in pyproject.toml and requirements.txt are bounded to
    the preview range this shim was validated against.
    """
    if not hasattr(_agent_framework, "__version__"):
        _agent_framework.__version__ = importlib.metadata.version("agent-framework-core")

    for name, module in {
        "Agent": "agent_framework._agents",
        "BaseAgent": "agent_framework._agents",
        "RawAgent": "agent_framework._agents",
        "SupportsAgentRun": "agent_framework._agents",
        "AgentMiddlewareLayer": "agent_framework._middleware",
        "AgentSession": "agent_framework._sessions",
        "BaseEmbeddingClient": "agent_framework._clients",
        "ChatOptions": "agent_framework._types",
        "ChatAndFunctionMiddlewareTypes": "agent_framework._middleware",
        "ChatMiddlewareLayer": "agent_framework._middleware",
        "ChatResponseUpdate": "agent_framework._types",
        "Content": "agent_framework._types",
        "ContextProvider": "agent_framework._sessions",
        "FileCheckpointStorage": "agent_framework._workflows._checkpoint",
        "Embedding": "agent_framework._types",
        "EmbeddingGenerationOptions": "agent_framework._types",
        "FunctionInvocationConfiguration": "agent_framework._tools",
        "FunctionInvocationLayer": "agent_framework._tools",
        "FunctionTool": "agent_framework._tools",
        "GeneratedEmbeddings": "agent_framework._types",
        "HistoryProvider": "agent_framework._sessions",
        "Message": "agent_framework._types",
        "SessionContext": "agent_framework._sessions",
        "UsageDetails": "agent_framework._types",
        "WorkflowAgent": "agent_framework._workflows._agent",
        "load_settings": "agent_framework._settings",
    }.items():
        if not hasattr(_agent_framework, name):
            setattr(_agent_framework, name, getattr(importlib.import_module(module), name))

