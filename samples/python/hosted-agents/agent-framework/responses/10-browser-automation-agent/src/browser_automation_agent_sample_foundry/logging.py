from __future__ import annotations

import re
import sys

BLUE = "\033[94m"
YELLOW = "\033[93m"
DIM = "\033[2m"
RESET = "\033[0m"


def color(message: str, code: str) -> str:
    return f"{code}{message}{RESET}"


def log_blue(message: str) -> None:
    print(color(message, BLUE), file=sys.stderr, flush=True)


def log_yellow(message: str) -> None:
    print(color(message, YELLOW), file=sys.stderr, flush=True)


def log_verbose(enabled: bool, message: str) -> None:
    if enabled:
        print(color(f"[verbose] {message}", DIM), file=sys.stderr, flush=True)


def redact_sensitive_values(text: str) -> str:
    text = re.sub(r"(Authorization:\s*Bearer\s+)[^\s\"']+", r"\1<redacted>", text, flags=re.IGNORECASE)
    text = re.sub(r"(--access-token\s+)(\"[^\"]+\"|'[^']+'|\S+)", r"\1<redacted>", text)
    text = re.sub(r"(access_token=)[^&\s\"']+", r"\1<redacted>", text, flags=re.IGNORECASE)
    text = re.sub(r"(accessToken=)[^&\s\"']+", r"\1<redacted>", text, flags=re.IGNORECASE)
    text = re.sub(r"(accessKey=)[^&\s\"']+", r"\1<redacted>", text, flags=re.IGNORECASE)
    text = re.sub(r"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b", "<redacted-jwt>", text)
    return re.sub(r"wss://[^\s\"']+", "wss://<redacted-cdp-url>", text)

