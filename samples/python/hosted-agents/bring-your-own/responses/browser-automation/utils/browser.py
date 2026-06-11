# Copyright (c) Microsoft. All rights reserved.

"""Browser session manager — drives playwright-cli via subprocess."""

from __future__ import annotations

import asyncio
import logging
import os
import shlex
import shutil
import sys
from pathlib import Path

logger = logging.getLogger(__name__)

from .utils import redact as _redact


class BrowserSession:
    """Manages a playwright-cli session against a remote CDP browser."""

    ALLOWED_COMMANDS = {
        "goto", "go-back", "go-forward", "reload",
        "snapshot", "screenshot",
        "click", "dblclick", "hover",
        "fill", "type", "press", "keys", "select", "check", "uncheck",
        "scroll", "eval",
        "tab-list", "tab-new", "tab-close", "tab-select",
        "wait", "state",
    }

    def __init__(self, session_id: str):
        self.session_id = session_id
        self._connected = False
        self._timeout = int(os.getenv("BROWSER_TIMEOUT_SECONDS", "180"))

    async def connect(self, cdp_url: str) -> dict:
        """Attach to a remote browser via CDP URL."""
        result = await self._exec(f"attach --cdp={cdp_url}")
        if result.get("success"):
            self._connected = True
            result["stdout"] = result.get("stdout") or "Connected successfully."
        return result

    async def run(self, command: str, args: list[str] | None = None) -> dict:
        """Run a playwright-cli command."""
        cmd = command.strip()
        cmd_args = args or []

        if cmd not in self.ALLOWED_COMMANDS:
            return {"success": False, "error": f"Unknown command: {cmd}. Allowed: {sorted(self.ALLOWED_COMMANDS)}"}
        if not self._connected:
            return {"success": False, "error": "Browser not connected. Session may need to be recreated."}

        full_cmd = cmd
        if cmd_args:
            quoted = [f'"{a}"' if " " in a else a for a in cmd_args]
            full_cmd += " " + " ".join(quoted)
        return await self._exec(full_cmd)

    async def close(self):
        """Detach from the browser session."""
        if self._connected:
            await self._exec("detach")
        self._connected = False

    async def _exec(self, command: str) -> dict:
        """Execute a playwright-cli subprocess."""
        env = os.environ.copy()
        env["PYTHONIOENCODING"] = "utf-8"
        env["PATH"] = str(Path(sys.executable).parent) + os.pathsep + env.get("PATH", "")
        cli = shutil.which("playwright-cli", path=env.get("PATH", "")) or "playwright-cli"
        parts = [cli, f"-s={self.session_id}"] + shlex.split(command)
        logger.info("[pw-cli] %s", _redact(" ".join(parts)))

        try:
            proc = await asyncio.create_subprocess_exec(
                *parts,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
                env=env,
            )
        except FileNotFoundError:
            return {"success": False, "error": f"playwright-cli not found at: {cli}"}

        try:
            stdout_b, stderr_b = await asyncio.wait_for(proc.communicate(), timeout=self._timeout)
        except asyncio.TimeoutError:
            proc.kill()
            return {"success": False, "error": "Command timed out"}

        stdout = _redact(stdout_b.decode("utf-8", errors="replace"))
        stderr = _redact(stderr_b.decode("utf-8", errors="replace"))
        success = proc.returncode == 0

        result = {"success": success, "stdout": stdout if stdout else None}
        if stderr:
            result["stderr"] = stderr
        return result
