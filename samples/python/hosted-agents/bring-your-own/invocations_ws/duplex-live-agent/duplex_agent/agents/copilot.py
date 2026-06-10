# Copyright (c) Microsoft. All rights reserved.

"""Copilot agent — GitHub Copilot SDK as a background coding agent."""

from __future__ import annotations

import asyncio
import logging
import os
import pathlib
import time
import uuid
from typing import Any, Callable

logger = logging.getLogger(__name__)

from copilot.session import ProviderConfig

from duplex_agent.base import AgentConfig, AgentSpec, AsyncTaskAgent, TaskEvent


class CopilotTaskAgent(AsyncTaskAgent):
    """Run a GitHub Copilot SDK session as a background task."""

    def __init__(
        self,
        task_id: str,
        output_queue: asyncio.Queue[TaskEvent],
        github_token: str | None = None,
        skills_dir: str | None = None,
        model: str | None = None,
        working_directory: str | None = None,
        provider: ProviderConfig | None = None,
    ):
        super().__init__(task_id, output_queue)
        self._github_token = github_token
        self._provider = provider
        self._skills_dir = skills_dir
        self._model = model
        self._working_directory = working_directory or self._default_working_dir()

    @staticmethod
    def _default_working_dir() -> str:
        if os.name == "nt":
            return os.environ.get("USERPROFILE", os.environ.get("HOME", "/home"))
        return os.environ.get("HOME", "/home")

    @classmethod
    def create_factory(
        cls,
        github_token: str | None = None,
        skills_dir: str | None = None,
        model: str | None = None,
        working_directory: str | None = None,
        name: str = "copilot",
        description: str = "GitHub Copilot coding agent",
        provider: ProviderConfig | None = None,
    ) -> Callable[[str, asyncio.Queue], "CopilotTaskAgent"]:
        """Build a factory function suitable for AgentSpec.factory."""

        def factory(task_id: str, queue: asyncio.Queue) -> "CopilotTaskAgent":
            agent = cls(
                task_id, queue, github_token, skills_dir, model,
                working_directory, provider,
            )
            agent.name = name
            agent.description = description
            return agent

        return factory
    
    def _approve_all(self, request, context):
        """Auto-approve all permission requests (no interactive user in container)."""
        from copilot.session import PermissionRequestResult
        return PermissionRequestResult(kind="approve-once")

    async def run(self, task_description: str) -> str:
        from copilot import CopilotClient, SubprocessConfig
        from copilot.generated.session_events import SessionEvent, SessionEventType

        await self.milestone("Starting Copilot session...")

        if self._provider:
            # BYOK mode: Foundry model via Managed Identity — no token needed.
            logger.info("Using BYOK provider with endpoint: %s", self._provider["base_url"])
            client = CopilotClient(auto_start=False)
        elif self._github_token:
            # Copilot mode: use GitHub token.
            logger.info("Using GitHub Copilot with provided token and model: %s", self._model)
            client = CopilotClient(
                SubprocessConfig(github_token=self._github_token), auto_start=False)
        else:
            raise RuntimeError("Set GITHUB_TOKEN to enable the Copilot background agent.")
        await client.start()

        session_id = str(uuid.uuid4())
        session_kwargs: dict[str, Any] = {
            "session_id": session_id,
            "on_permission_request": self._approve_all,
            "streaming": True,
            "working_directory": self._working_directory,
        }
        if self._skills_dir:
            session_kwargs["skill_directories"] = [self._skills_dir]
        if self._model:
            session_kwargs["model"] = self._model
        if self._provider:
            session_kwargs["provider"] = self._provider

        try:
            session = await client.create_session(**session_kwargs)
        except Exception:
            await client.stop()
            raise

        # Collect response via event queue
        loop = asyncio.get_running_loop()
        queue: asyncio.Queue[Any] = asyncio.Queue()
        result_chunks: list[str] = []

        last_tool_label = ""
        last_tool_time = 0.0

        def _tool_name(event_data) -> str:
            return (
                getattr(event_data, "tool_name", None)
                or getattr(event_data, "mcp_tool_name", None)
                or "tool"
            )

        def _describe_tool_start(event_data) -> str:
            """Build a descriptive label from tool name + arguments."""
            name = _tool_name(event_data)
            args = getattr(event_data, "arguments", None)
            if isinstance(args, str):
                try:
                    import json as _json
                    args = _json.loads(args)
                except (ValueError, TypeError):
                    args = None

            if name == "view" and isinstance(args, dict):
                path = args.get("filePath") or args.get("path") or ""
                if path:
                    short = pathlib.PurePosixPath(path).name or path
                    return f"Reading {short}"
                return "Reading file"
            elif name == "glob" and isinstance(args, dict):
                pattern = args.get("pattern") or args.get("glob") or ""
                return f"Searching files: {pattern}" if pattern else "Searching files"
            elif name == "grep" and isinstance(args, dict):
                pattern = args.get("pattern") or args.get("query") or ""
                include = args.get("include") or ""
                desc = f"Searching for '{pattern}'" if pattern else "Searching code"
                if include:
                    desc += f" in {include}"
                return desc
            elif name in ("bash", "powershell") and isinstance(args, dict):
                cmd = args.get("command") or ""
                if cmd:
                    short_cmd = cmd.split("\n")[0][:80]
                    return f"Running: {short_cmd}"
                return "Running command"
            elif name == "python" and isinstance(args, dict):
                code = args.get("code") or ""
                first_line = code.split("\n")[0][:60] if code else ""
                return f"Running Python: {first_line}" if first_line else "Running Python"
            elif name == "node" and isinstance(args, dict):
                code = args.get("code") or ""
                first_line = code.split("\n")[0][:60] if code else ""
                return f"Running Node.js: {first_line}" if first_line else "Running Node.js"
            elif name == "create" and isinstance(args, dict):
                path = args.get("filePath") or args.get("path") or ""
                if path:
                    short = pathlib.PurePosixPath(path).name or path
                    return f"Creating {short}"
                return "Creating file"
            elif name == "edit" and isinstance(args, dict):
                path = args.get("filePath") or args.get("path") or ""
                if path:
                    short = pathlib.PurePosixPath(path).name or path
                    return f"Editing {short}"
                return "Editing file"
            elif name == "report_intent" and isinstance(args, dict):
                intent = args.get("intent") or args.get("title") or ""
                return f"Planning: {intent}" if intent else "Planning"
            else:
                # MCP or unknown tools — include server context if available
                mcp_server = getattr(event_data, "mcp_server_name", None)
                if mcp_server:
                    return f"Using {mcp_server}/{name}"
                return f"Using {name}"

        def event_handler(event: SessionEvent) -> None:
            nonlocal last_tool_label, last_tool_time
            etype = event.type

            if etype == SessionEventType.ASSISTANT_MESSAGE_DELTA:
                if event.data.delta_content:
                    loop.call_soon_threadsafe(
                        queue.put_nowait, ("delta", event.data.delta_content)
                    )

            elif etype == SessionEventType.ASSISTANT_MESSAGE:
                content = getattr(event.data, "content", None) or ""
                if content.strip():
                    loop.call_soon_threadsafe(queue.put_nowait, ("message", content))

            elif etype == SessionEventType.TOOL_EXECUTION_START:
                label = _describe_tool_start(event.data)
                now = time.monotonic()
                if label != last_tool_label or (now - last_tool_time) >= 5.0:
                    last_tool_label = label
                    last_tool_time = now
                    loop.call_soon_threadsafe(
                        queue.put_nowait, ("tool_start", label)
                    )

            elif etype == SessionEventType.TOOL_EXECUTION_PROGRESS:
                msg = getattr(event.data, "progress_message", None)
                if msg:
                    loop.call_soon_threadsafe(
                        queue.put_nowait, ("tool_progress", msg)
                    )

            elif etype == SessionEventType.TOOL_EXECUTION_COMPLETE:
                success = getattr(event.data, "success", True)
                if not success:
                    err = getattr(event.data, "error", None)
                    err_msg = getattr(err, "message", None) if err else None
                    loop.call_soon_threadsafe(
                        queue.put_nowait, ("tool_error", err_msg or "Tool failed")
                    )

            elif etype == SessionEventType.SESSION_IDLE:
                loop.call_soon_threadsafe(queue.put_nowait, ("done", None))

            elif etype == SessionEventType.SESSION_ERROR:
                error_msg = getattr(event.data, "message", None) or "Session error"
                loop.call_soon_threadsafe(
                    queue.put_nowait, ("error", error_msg)
                )

        unsubscribe = session.on(event_handler)

        try:
            await session.send(task_description, mode="immediate")

            while True:
                try:
                    item = await asyncio.wait_for(queue.get(), timeout=30.0)
                except asyncio.TimeoutError:
                    await self.milestone("Still working...")
                    continue

                kind, data = item
                if kind == "delta":
                    result_chunks.append(data)
                elif kind == "message":
                    result_chunks.append(data)
                elif kind == "tool_start":
                    await self.milestone(f"{data}...")
                elif kind == "tool_progress":
                    await self.milestone(data)
                elif kind == "tool_error":
                    await self.milestone(f"⚠ {data}")
                elif kind == "done":
                    break
                elif kind == "error":
                    raise RuntimeError(data)

                # Check for mid-flight updates from the user
                if self._updates:
                    update = self._updates.pop(0)
                    await session.send(
                        f"[User update]: {update}", mode="immediate"
                    )

        finally:
            unsubscribe()
            try:
                await session.stop()
            except Exception:
                pass
            try:
                await client.stop()
            except Exception:
                pass

        return "".join(result_chunks) or "(no result)"


class CopilotAgent:
    """Builder for the GitHub Copilot sub-agent."""

    @classmethod
    def build(cls, config: AgentConfig) -> AgentSpec | None:
        """Construct the copilot AgentSpec, or None if neither mode is configured."""
        github_token = os.environ.get("GITHUB_TOKEN", "").strip() or None

        if not github_token:
            logger.warning(
                "CopilotAgent: No GITHUB_TOKEN found; skipping GitHub Copilot mode."
                )
            return None

        copilot_model = os.environ.get("GITHUB_COPILOT_MODEL", "").strip() or None
        return AgentSpec(
            name="copilot",
            description=(
                "GitHub Copilot coding agent. Use for programming tasks: writing code, "
                "debugging, searching codebases, running commands, and file operations, "
                "and searching and analyzing news, documents, earnings reports, etc."
            ),
            factory=CopilotTaskAgent.create_factory(
                github_token=github_token,
                skills_dir=config.skills_dir,
                model=copilot_model,
                provider=None,
                name="copilot",
                description="GitHub Copilot coding agent",
            ),
        )
