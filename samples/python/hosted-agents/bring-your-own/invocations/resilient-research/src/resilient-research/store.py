"""Checkpoint store for resilient research application state.

This checkpoint store is backed by the **Foundry StateStore**
(:class:`FoundryStateStore`) when ``FOUNDRY_PROJECT_ENDPOINT`` is
configured, so application state survives container restarts. When no
endpoint is configured (the offline demo mode), it falls back to atomic
local files so the sample still runs with no credentials.

Each invocation has one item containing its phase watermarks, in-flight
text, or terminal status. The item uses the durable-task recovery lifetime
so recovery data cannot expire before the task does.
"""

from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path
from typing import Any

_INVOCATION_STORE_NAME = "research-invocations"
_INVOCATION_ITEM_TTL_SECONDS = 30 * 24 * 60 * 60


class CheckpointStore:
    """Durable invocation checkpoint store with a local-file fallback."""

    def __init__(self, base_dir: Path) -> None:
        self._base = base_dir
        self._base.mkdir(parents=True, exist_ok=True)
        # Use the Foundry StateStore when an endpoint is available; otherwise
        # fall back to the local file store for the offline demo.
        self._use_state_store = bool(os.environ.get("FOUNDRY_PROJECT_ENDPOINT"))
        self._store: Any = None

    async def _state_store(self) -> Any:
        """Lazily resolve the invocation state store."""
        if self._store is None:
            from azure.ai.agentserver.core.storage import (  # pylint: disable=import-outside-toplevel
                FoundryStateStore,
            )

            self._store = await FoundryStateStore.get_or_create(
                _INVOCATION_STORE_NAME,
                item_ttl_seconds=_INVOCATION_ITEM_TTL_SECONDS,
            )
        return self._store

    async def _get_item(self, key: str) -> Any:
        if self._use_state_store:
            store = await self._state_store()
            item = await store.get_item(key)
            return item.value if item is not None else None

        path = self._path(key)
        if not path.exists():
            return None
        return json.loads(path.read_text(encoding="utf-8"))

    async def _set_item(self, key: str, value: Any) -> None:
        if self._use_state_store:
            store = await self._state_store()
            await store.set_item(key, value)
            return

        await self._write_local(key, value)

    async def get_checkpoint(self, invocation_id: str) -> dict[str, Any]:
        """Return one invocation's combined recovery record."""
        value = await self._get_item(invocation_id)
        return dict(value) if isinstance(value, dict) else {}

    async def get_state(self, invocation_id: str) -> dict[str, Any]:
        """Return an invocation's application watermarks."""
        checkpoint = await self.get_checkpoint(invocation_id)
        state = checkpoint.get("state")
        return dict(state) if isinstance(state, dict) else {}

    async def put_checkpoint(
        self,
        invocation_id: str,
        state: dict[str, Any],
        text: str,
    ) -> None:
        """Replace an invocation's watermarks and text in one write."""
        await self._set_item(
            invocation_id,
            {
                "state": dict(state),
                "text": text,
            },
        )

    async def get_terminal_status(
        self,
        invocation_id: str,
    ) -> dict[str, Any] | None:
        """Return an invocation's terminal marker, if present."""
        checkpoint = await self.get_checkpoint(invocation_id)
        marker = checkpoint.get("terminal_status")
        return dict(marker) if isinstance(marker, dict) else None

    async def put_terminal_status(
        self,
        invocation_id: str,
        status: str,
        error: dict[str, Any] | None = None,
    ) -> None:
        """Replace an invocation's checkpoint with one terminal marker."""
        marker: dict[str, Any] = {"status": status}
        if error is not None:
            marker["error"] = error
        await self._set_item(invocation_id, {"terminal_status": marker})

    async def _write_local(self, key: str, value: Any) -> None:
        target = self._path(key)
        fd, tmp = tempfile.mkstemp(
            dir=str(self._base),
            prefix=f"{key}_",
            suffix=".tmp",
        )
        try:
            with open(fd, "w", encoding="utf-8") as fh:
                json.dump(value, fh)
            Path(tmp).replace(target)
        except BaseException:
            Path(tmp).unlink(missing_ok=True)
            raise

    def _path(self, key: str) -> Path:
        return self._base / f"{key}.json"
