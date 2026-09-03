"""Checkpoint store for resilient research application state.

This checkpoint store is backed by the **Foundry StateStore**
(:class:`FoundryStateStore`) when ``FOUNDRY_PROJECT_ENDPOINT`` is
configured — i.e. hosted deployments and real local runs — so the
phase watermarks and in-flight text survive container restarts. When no
endpoint is configured (the offline demo mode), it falls back to atomic
local files so the sample still runs with no credentials.

Text checkpoints are keyed by ``invocation_id`` and expire after an hour.
Watermark dictionaries are keyed by the durable task id in a separate store
whose retention matches the default durable-storage lifetime.
"""

from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path
from typing import Any

# In-flight text is disposable after an hour, but recovery watermarks must
# outlive a long deployment outage. Keep them in separate stores because a
# Foundry State Store's TTL is fixed when the store is first created.
_TEXT_STORE_NAME = "research-checkpoints"
_TEXT_ITEM_TTL_SECONDS = 3600
_TASK_STATE_STORE_NAME = "research-task-watermarks"
_TASK_STATE_TTL_SECONDS = 30 * 24 * 60 * 60


class CheckpointStore:
    """Durable key->text checkpoint store (StateStore-backed, file fallback)."""

    def __init__(self, base_dir: Path) -> None:
        self._base = base_dir
        self._base.mkdir(parents=True, exist_ok=True)
        # Use the Foundry StateStore when an endpoint is available; otherwise
        # fall back to the local file store for the offline demo.
        self._use_state_store = bool(os.environ.get("FOUNDRY_PROJECT_ENDPOINT"))
        self._text_store: Any = None
        self._task_state_store: Any = None

    async def _text_state_store(self) -> Any:
        """Lazily resolve the short-lived in-flight text store."""
        if self._text_store is None:
            from azure.ai.agentserver.core.storage import (  # pylint: disable=import-outside-toplevel
                FoundryStateStore,
            )

            self._text_store = await FoundryStateStore.get_or_create(
                _TEXT_STORE_NAME,
                item_ttl_seconds=_TEXT_ITEM_TTL_SECONDS,
            )
        return self._text_store

    async def _task_watermark_store(self) -> Any:
        """Lazily resolve the durable task-watermark store."""
        if self._task_state_store is None:
            from azure.ai.agentserver.core.storage import (  # pylint: disable=import-outside-toplevel
                FoundryStateStore,
            )

            self._task_state_store = await FoundryStateStore.get_or_create(
                _TASK_STATE_STORE_NAME,
                item_ttl_seconds=_TASK_STATE_TTL_SECONDS,
            )
        return self._task_state_store

    async def get(self, key: str) -> str:
        """Return the stored text, or empty string if absent."""
        if self._use_state_store:
            store = await self._text_state_store()
            item = await store.get_item(key)
            if item is None:
                return ""
            value = item.value or {}
            return str(value.get("text", ""))

        path = self._path(key)
        if not path.exists():
            return ""
        return json.loads(path.read_text(encoding="utf-8"))

    async def put(self, key: str, value: str) -> None:
        """Store *value* under *key* (create-or-replace)."""
        if self._use_state_store:
            store = await self._text_state_store()
            # StateStore item values are JSON objects, so wrap the text.
            await store.set_item(key, {"text": value})
            return

        await self._write_local(key, value)

    async def delete(self, key: str) -> None:
        """Remove *key* if present; no-op otherwise."""
        if self._use_state_store:
            from azure.ai.agentserver.core.storage import (  # pylint: disable=import-outside-toplevel
                FoundryStorageNotFoundError,
            )

            store = await self._text_state_store()
            try:
                await store.delete_item(key)
            except FoundryStorageNotFoundError:
                pass
            return

        path = self._path(key)
        if path.exists():
            path.unlink()

    async def get_state(self, task_id: str) -> dict[str, Any]:
        """Return the task's application watermarks, or an empty mapping."""
        key = f"state-{task_id}"
        if self._use_state_store:
            store = await self._task_watermark_store()
            item = await store.get_item(key)
            return dict(item.value) if item is not None and isinstance(item.value, dict) else {}

        path = self._path(key)
        if not path.exists():
            return {}
        value = json.loads(path.read_text(encoding="utf-8"))
        return dict(value) if isinstance(value, dict) else {}

    async def put_state(self, task_id: str, state: dict[str, Any]) -> None:
        """Persist the task's application watermarks."""
        key = f"state-{task_id}"
        if self._use_state_store:
            store = await self._task_watermark_store()
            await store.set_item(key, state)
            return

        await self._write_local(key, state)

    async def delete_state(self, task_id: str) -> None:
        """Remove the task's application watermarks."""
        key = f"state-{task_id}"
        if self._use_state_store:
            from azure.ai.agentserver.core.storage import (  # pylint: disable=import-outside-toplevel
                FoundryStorageNotFoundError,
            )

            store = await self._task_watermark_store()
            try:
                await store.delete_item(key)
            except FoundryStorageNotFoundError:
                pass
            return

        path = self._path(key)
        if path.exists():
            path.unlink()

    async def _write_local(self, key: str, value: Any) -> None:
        target = self._path(key)
        fd, tmp = tempfile.mkstemp(dir=str(self._base), prefix=f"{key}_", suffix=".tmp")
        try:
            with open(fd, "w", encoding="utf-8") as fh:
                json.dump(value, fh)
            Path(tmp).replace(target)
        except BaseException:
            Path(tmp).unlink(missing_ok=True)
            raise

    def _path(self, key: str) -> Path:
        return self._base / f"{key}.json"
