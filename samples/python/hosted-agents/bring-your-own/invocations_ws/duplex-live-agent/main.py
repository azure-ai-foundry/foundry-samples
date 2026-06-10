# Copyright (c) Microsoft. All rights reserved.

"""Duplex Live Agent — entry point.
"""

from __future__ import annotations

import logging
import logging.handlers
import os
from urllib.parse import urlsplit, urlunsplit

try:
    from dotenv import load_dotenv
except ImportError:

    def load_dotenv(*_a, **_kw):
        return False


load_dotenv(override=False)

from starlette.websockets import WebSocket, WebSocketDisconnect

from azure.ai.agentserver.invocations import InvocationAgentServerHost

from duplex_agent import DuplexLiveAgent
from duplex_agent.agents import AgentConfig, load_agents
from duplex_agent.routers.realtime_router import RealtimeRouter

logger = logging.getLogger("duplex-live-agent")
logging.basicConfig(
    level=os.environ.get("LOG_LEVEL", "INFO"),
    format="%(asctime)s %(name)s %(levelname)s: %(message)s",
)

# --- File logging: write detailed turn logs to rotating files ---
_log_dir = os.environ.get("DUPLEX_LOG_DIR", "logs")
os.makedirs(_log_dir, exist_ok=True)

_file_formatter = logging.Formatter(
    "%(asctime)s %(name)s %(levelname)s: %(message)s"
)

# Realtime router I/O (user speech, bot replies, tool calls)
_rt_file_handler = logging.handlers.RotatingFileHandler(
    os.path.join(_log_dir, "realtime_turns.log"),
    maxBytes=10 * 1024 * 1024,  # 10 MB
    backupCount=5,
    encoding="utf-8",
)
_rt_file_handler.setLevel(logging.INFO)
_rt_file_handler.setFormatter(_file_formatter)
_realtime_io_logger = logging.getLogger("duplex_agent.realtime_io")
_realtime_io_logger.addHandler(_rt_file_handler)
_realtime_io_logger.setLevel(logging.INFO)

# LLM calls (handoff workflow requests/responses)
_llm_file_handler = logging.handlers.RotatingFileHandler(
    os.path.join(_log_dir, "llm_calls.log"),
    maxBytes=10 * 1024 * 1024,
    backupCount=5,
    encoding="utf-8",
)
_llm_file_handler.setLevel(logging.INFO)
_llm_file_handler.setFormatter(_file_formatter)
_llm_calls_logger = logging.getLogger("duplex_agent.llm_calls")
_llm_calls_logger.addHandler(_llm_file_handler)
_llm_calls_logger.setLevel(logging.INFO)

# Workflow events
_evt_file_handler = logging.handlers.RotatingFileHandler(
    os.path.join(_log_dir, "workflow_events.log"),
    maxBytes=10 * 1024 * 1024,
    backupCount=5,
    encoding="utf-8",
)
_evt_file_handler.setLevel(logging.INFO)
_evt_file_handler.setFormatter(_file_formatter)
_workflow_events_logger = logging.getLogger("duplex_agent.workflow_events")
_workflow_events_logger.addHandler(_evt_file_handler)
_workflow_events_logger.setLevel(logging.INFO)

# Enable detailed LLM request/response logging with LOG_LEVEL=DEBUG or:
#   DUPLEX_EVENT_LOG_LEVEL=DEBUG to see every workflow event
#   DUPLEX_REALTIME_LOG_LEVEL=DEBUG to see all realtime router I/O (user text, bot text, tool calls, injections)
_llm_log_level = os.environ.get("DUPLEX_LLM_LOG_LEVEL", "").strip()
if _llm_log_level:
    logging.getLogger("duplex_agent.llm_calls").setLevel(getattr(logging, _llm_log_level, logging.INFO))
_event_log_level = os.environ.get("DUPLEX_EVENT_LOG_LEVEL", "").strip()
if _event_log_level:
    logging.getLogger("duplex_agent.workflow_events").setLevel(getattr(logging, _event_log_level, logging.INFO))
_rt_log_level = os.environ.get("DUPLEX_REALTIME_LOG_LEVEL", "").strip()
if _rt_log_level:
    logging.getLogger("duplex_agent.realtime_io").setLevel(getattr(logging, _rt_log_level, logging.INFO))


# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------


def _resolve_endpoint() -> str:
    """Return the Voice Live AI-Services account URL."""
    raw = (
        os.environ.get("FOUNDRY_PROJECT_ENDPOINT", "").strip()
        or os.environ.get("AZURE_VOICELIVE_ENDPOINT", "").strip()
    )
    if not raw:
        raise EnvironmentError(
            "Set AZURE_VOICELIVE_ENDPOINT or FOUNDRY_PROJECT_ENDPOINT."
        )
    parts = urlsplit(raw)
    return urlunsplit((parts.scheme, parts.netloc, "/", "", ""))


ENDPOINT = _resolve_endpoint()
REALTIME_MODEL = os.environ.get("AZURE_VOICELIVE_MODEL", "gpt-realtime").strip()
VOICE = os.environ.get("AZURE_VOICELIVE_VOICE", "en-US-Ava:DragonHDLatestNeural").strip()

# Task model for background agents
TASK_MODEL = os.environ.get("AZURE_TASK_MODEL", "gpt-4o-mini").strip()
PROJECT_ENDPOINT = os.environ.get("FOUNDRY_PROJECT_ENDPOINT", "").strip()


# ---------------------------------------------------------------------------
# Agent assembly
# ---------------------------------------------------------------------------

import pathlib

_skills_path = pathlib.Path(__file__).parent / "skills"
SKILLS_DIR = str(_skills_path) if _skills_path.is_dir() else None

duplex_agent = DuplexLiveAgent(
    router_class=RealtimeRouter,
    router_kwargs={
        "endpoint": ENDPOINT,
        "model": REALTIME_MODEL,
        "voice": VOICE,
    },
    subagents=load_agents(
        AgentConfig(
            endpoint=PROJECT_ENDPOINT,
            model=TASK_MODEL,
            skills_dir=SKILLS_DIR,
        )
    ),
)


# ---------------------------------------------------------------------------
# Server
# ---------------------------------------------------------------------------

app = InvocationAgentServerHost()


@app.ws_handler
async def handle_ws(websocket: WebSocket) -> None:
    """Each WebSocket connection becomes a duplex session."""
    try:
        await duplex_agent.handle_session(websocket)
    except WebSocketDisconnect:
        return


if __name__ == "__main__":
    app.run()
