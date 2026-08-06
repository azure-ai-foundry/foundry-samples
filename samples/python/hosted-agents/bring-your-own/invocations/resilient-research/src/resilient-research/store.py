"""Checkpoint store for in-flight LLM content.

``ctx.metadata`` on the resilient-task primitive is a *small-watermark*
store, not a bulk-data store (see ``core/docs/tasks-guide.md``
§"Persistence Model"). For anything heavier than a few bytes — e.g.
the partially-streamed text of the current phase's in-flight subcall
chain — the application keeps its own checkpoint store and holds only a
*reference* (the per-turn ``invocation_id``) in metadata.

This checkpoint store is backed by the **Foundry StateStore**
(:class:`FoundryStateStore`) when ``FOUNDRY_PROJECT_ENDPOINT`` is
configured — i.e. hosted deployments and real local runs — so the
in-flight text survives container restarts through the same durable,
platform-managed store used for other agent state. When no endpoint is
configured (the offline demo mode), it falls back to an atomic local-file
store so the sample still runs with no credentials.

Both backings expose the same tiny async interface — ``get`` / ``put`` /
``delete`` keyed by ``invocation_id``.
"""

from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path
from typing import Any

# One store per agent for its in-flight research checkpoints. Item keys are the
# per-turn invocation ids; items expire after an hour so abandoned checkpoints
# do not accumulate.
_STORE_NAME = "research-checkpoints"
_ITEM_TTL_SECONDS = 3600


class CheckpointStore:
    """Durable key->text checkpoint store (StateStore-backed, file fallback)."""

    def __init__(self, base_dir: Path) -> None:
        self._base = base_dir
        self._base.mkdir(parents=True, exist_ok=True)
        # Use the Foundry StateStore when an endpoint is available; otherwise
        # fall back to the local file store for the offline demo.
        self._use_state_store = bool(os.environ.get("FOUNDRY_PROJECT_ENDPOINT"))
        self._store: Any = None

    async def _state_store(self) -> Any:
        """Lazily resolve (or create) the Foundry StateStore for this agent."""
        if self._store is None:
            from azure.ai.agentserver.core.storage import (  # pylint: disable=import-outside-toplevel
                FoundryStateStore,
            )

            self._store = await FoundryStateStore.get_or_create(
                _STORE_NAME,
                item_ttl_seconds=_ITEM_TTL_SECONDS,
            )
        return self._store

    async def get(self, key: str) -> str:
        """Return the stored text, or empty string if absent."""
        if self._use_state_store:
            store = await self._state_store()
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
            store = await self._state_store()
            # StateStore item values are JSON objects, so wrap the text.
            await store.set_item(key, {"text": value})
            return

        target = self._path(key)
        fd, tmp = tempfile.mkstemp(dir=str(self._base), prefix=f"{key}_", suffix=".tmp")
        try:
            with open(fd, "w", encoding="utf-8") as fh:
                json.dump(value, fh)
            Path(tmp).replace(target)
        except BaseException:
            Path(tmp).unlink(missing_ok=True)
            raise

    async def delete(self, key: str) -> None:
        """Remove *key* if present; no-op otherwise."""
        if self._use_state_store:
            from azure.ai.agentserver.core.storage import (  # pylint: disable=import-outside-toplevel
                FoundryStorageNotFoundError,
            )

            store = await self._state_store()
            try:
                await store.delete_item(key)
            except FoundryStorageNotFoundError:
                pass
            return

        path = self._path(key)
        if path.exists():
            path.unlink()

    def _path(self, key: str) -> Path:
        return self._base / f"{key}.json"

