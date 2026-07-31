# Copyright (c) Microsoft. All rights reserved.
"""Download files the user shares in Teams and hand them to the Copilot SDK.

When a user attaches a file, Teams sends a ``message`` activity with an
attachment of type ``application/vnd.microsoft.teams.file.download.info`` whose
``content.downloadUrl`` is a pre-authenticated URL. We download the **raw
bytes** to a per-conversation folder under ``$HOME`` and hand the file *path*
straight to the model as an attachment — we do **no** parsing/extraction, so any
file type (PDF, PPTX, images, spreadsheets, code, …) is the model's to read and
reason over using its own tools.
"""

from __future__ import annotations

import json
import logging
import os
import re
from pathlib import Path
from typing import Any, Callable, Optional

logger = logging.getLogger("github-copilot.files")

# Raw shared files land here (one folder per conversation) so the model can open
# them by path via its file tools.
_HOME = os.environ.get("HOME") or "."
_FILES_DIR = Path(_HOME) / ".github-copilot" / "files"

# A progress sink: short human-readable status lines while a file downloads.
Progress = Optional[Callable[[str], None]]


def _emit(on_progress: Progress, text: str) -> None:
    if on_progress is not None:
        try:
            on_progress(text)
        except Exception:  # pylint: disable=broad-exception-caught
            pass


def _conv_dir(conversation_id: str) -> Path:
    safe = re.sub(r"[^A-Za-z0-9_-]", "_", conversation_id) or "default"
    return _FILES_DIR / safe


async def download_shared_files(
    activity: Any,
    conversation_id: str,
    on_progress: Progress = None,
    connector: Any = None,
) -> list[dict[str, str]]:
    """Download each shared file/image verbatim and return item dicts.

    Each item is ``{name, path, kind}`` where ``kind`` is ``"file"`` or
    ``"image"``; image items also carry a ``mime`` field. No extraction: raw
    bytes are written to disk and handed to the model (files by path, images as
    inline base64 for vision).

    Teams file uploads arrive as ``file.download.info`` with a pre-authenticated
    ``downloadUrl``. M365 Copilot images arrive as ``image/*`` with a Bot
    Connector ``content_url`` (``.../v3/attachments/{id}/views/{view}``) that
    requires the authenticated ``connector`` (from the turn context) to fetch.
    """
    import httpx

    out: list[dict[str, str]] = []
    dest = _conv_dir(conversation_id)
    dest.mkdir(parents=True, exist_ok=True)
    async with httpx.AsyncClient(timeout=60, follow_redirects=True) as client:
        for att in getattr(activity, "attachments", None) or []:
            # Any attachment that carries a pre-authenticated download URL is a
            # shared file — download it verbatim (Teams paperclip uploads).
            url = _download_url(att)
            if url:
                name = getattr(att, "name", None) or "attachment"
                try:
                    _emit(on_progress, f"Downloading {name}…")
                    resp = await client.get(url)
                    resp.raise_for_status()
                    safe_name = re.sub(r"[^A-Za-z0-9._-]", "_", name) or "file"
                    path = dest / safe_name
                    path.write_bytes(resp.content)
                    out.append({"name": name, "path": str(path), "kind": "file"})
                except Exception as ex:  # pylint: disable=broad-exception-caught
                    logger.warning("Could not download %s: %s", name, ex)
                continue

            # An image comes as ``image/*`` with a Bot Connector content_url that
            # needs the authenticated connector to fetch (Teams + Copilot).
            if (getattr(att, "content_type", "") or "").lower().startswith("image/"):
                img = await _download_connector_image(att, dest, on_progress, connector)
                if img:
                    out.append(img)
    return out


def _parse_attachment_ref(url: str) -> tuple[str, str] | None:
    """Extract (attachment_id, view_id) from a Bot Connector attachment URL."""
    m = re.search(r"/v3/attachments/([^/]+)/views/([^/?]+)", url or "")
    return (m.group(1), m.group(2)) if m else None


async def _download_connector_image(
    att: Any, dest: Path, on_progress: Progress, connector: Any
) -> dict[str, str] | None:
    """Fetch an inline image via the authenticated turn connector client."""
    url = getattr(att, "content_url", None) or ""
    ref = _parse_attachment_ref(url)
    if not ref:
        logger.warning("IMAGE | unrecognized content_url=%r", url)
        return None
    if connector is None:
        logger.warning("IMAGE | no connector on turn; cannot fetch %s", url)
        _emit(on_progress, "Couldn't read the image (no channel connector).")
        return None
    attachment_id, view_id = ref
    try:
        _emit(on_progress, "Reading the image…")
        bio = await connector.attachments.get_attachment(attachment_id, view_id)
        data = bio.getvalue() if hasattr(bio, "getvalue") else bytes(bio)
        # The attachment already declares its type (this branch only runs for
        # ``image/*``); use it directly for the vision blob's mimeType.
        mime = (getattr(att, "content_type", None) or "image/png").lower()
        ext = {"image/png": ".png", "image/jpeg": ".jpg", "image/gif": ".gif", "image/webp": ".webp"}.get(mime, ".img")
        name = getattr(att, "name", None) or f"image{ext}"
        safe_name = re.sub(r"[^A-Za-z0-9._-]", "_", name) or f"image{ext}"
        path = dest / safe_name
        path.write_bytes(data)
        return {"name": name, "path": str(path), "kind": "image", "mime": mime}
    except Exception as ex:  # pylint: disable=broad-exception-caught
        logger.warning("Could not fetch image %s: %s", attachment_id, ex)
        _emit(on_progress, "Couldn't read the image.")
        return None


def _download_url(att: Any) -> str | None:
    """Return the pre-authenticated download URL for a Teams file attachment."""
    content = getattr(att, "content", None)
    if isinstance(content, str):
        try:
            content = json.loads(content)
        except (json.JSONDecodeError, ValueError):
            return None
    elif content is not None and not isinstance(content, dict):
        dump = getattr(content, "model_dump", None)
        content = dump(by_alias=True) if callable(dump) else None
    return content.get("downloadUrl") if isinstance(content, dict) else None

