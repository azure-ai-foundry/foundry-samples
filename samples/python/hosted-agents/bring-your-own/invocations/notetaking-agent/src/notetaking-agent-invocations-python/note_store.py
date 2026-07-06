# Copyright (c) Microsoft. All rights reserved.

"""Thread-safe per-session note storage using JSONL files."""

import json
import os
import threading
from dataclasses import dataclass
from datetime import datetime, timezone


@dataclass
class NoteEntry:
    """A single note with its creation timestamp."""

    note: str
    timestamp: str


_lock = threading.Lock()


def _get_file_path(session_id: str) -> str:
    """Return the JSONL file path for a given session.

    Files are stored under $HOME so they are accessible via the Session Files API.
    """
    safe_id = "".join(c if c.isalnum() or c in "-_" else "_" for c in session_id)
    base_dir = os.environ.get("HOME", os.getcwd())
    return os.path.join(base_dir, f"notes_{safe_id}.jsonl")


def save_note(session_id: str, note_text: str) -> NoteEntry:
    """Append a note to the session's JSONL file."""
    entry = NoteEntry(note=note_text, timestamp=datetime.now(timezone.utc).isoformat())
    line = json.dumps({"note": entry.note, "timestamp": entry.timestamp})
    with _lock:
        with open(_get_file_path(session_id), "a") as f:
            f.write(line + "\n")
    return entry


def get_notes(session_id: str) -> list[NoteEntry]:
    """Read all notes from the session's JSONL file."""
    path = _get_file_path(session_id)
    with _lock:
        if not os.path.exists(path):
            return []
        with open(path) as f:
            entries = []
            for line in f:
                line = line.strip()
                if line:
                    try:
                        data = json.loads(line)
                        entries.append(NoteEntry(note=data["note"], timestamp=data["timestamp"]))
                    except (json.JSONDecodeError, KeyError):
                        continue
            return entries
