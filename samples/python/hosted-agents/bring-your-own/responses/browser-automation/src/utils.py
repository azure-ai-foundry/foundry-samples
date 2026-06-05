# Copyright (c) Microsoft. All rights reserved.

"""Shared utilities — token/secret redaction for safe logging."""

import re

_REDACT_PATTERNS = [
    (re.compile(r"(accessKey=)[^&\s\"']+", re.IGNORECASE), r"\1<redacted>"),
    (re.compile(r"\beyJ[a-zA-Z0-9._-]{20,}\b"), "<token>"),
]


def redact(text: str) -> str:
    """Redact tokens and secrets from text for safe logging/display."""
    for pat, rep in _REDACT_PATTERNS:
        text = pat.sub(rep, text)
    return text
