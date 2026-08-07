"""Encode and decode the validation dashboard's hidden issue state."""

from __future__ import annotations

import base64
import binascii
import json
import re
from typing import Any


MARKER_PREFIX = "<!-- validation-health-state-v1:"
MARKER_PATTERN = re.compile(r"<!-- validation-health-state-v1:(.*?) -->", re.DOTALL)
ENCODED_STATE_PATTERN = re.compile(r"^[A-Za-z0-9_=-]+$")


class StateError(ValueError):
    """Raised when hidden dashboard state is malformed."""


def encode_state(payload: dict[str, Any]) -> str:
    serialized = json.dumps(payload, separators=(",", ":"), sort_keys=True).encode("utf-8")
    encoded = base64.urlsafe_b64encode(serialized).decode("ascii")
    return f"<!-- validation-health-state-v1:{encoded} -->"


def extract_state(markdown: str) -> dict[str, Any] | None:
    marker_count = markdown.count(MARKER_PREFIX)
    if marker_count == 0:
        return None
    if marker_count != 1:
        raise StateError("dashboard body must contain at most one hidden state marker")
    match = MARKER_PATTERN.search(markdown)
    if match is None or not ENCODED_STATE_PATTERN.fullmatch(match.group(1)):
        raise StateError("dashboard hidden state marker is invalid")
    try:
        decoded = base64.urlsafe_b64decode(match.group(1).encode("ascii"))
        payload = json.loads(decoded)
    except (binascii.Error, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise StateError("dashboard hidden state marker is invalid") from exc
    if not isinstance(payload, dict):
        raise StateError("dashboard hidden state must be a JSON object")
    return payload
