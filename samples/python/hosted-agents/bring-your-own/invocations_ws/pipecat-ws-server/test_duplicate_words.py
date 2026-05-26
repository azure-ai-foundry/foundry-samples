# Copyright (c) Microsoft. All rights reserved.

"""
Reproduction test for the bot-text duplication issue.

Connects directly to the bot WebSocket server (default `ws://localhost:8088/invocations_ws`),
using the same protobuf frame protocol the web_portal proxy uses, sends a
sequence of text inputs without waiting for turn-end (fixed cadence), and
captures the streaming bot output via the standard RTVI `bot-output` event.
After each conversation, it scans the bot's output for duplicated adjacent
words / repeated phrases and reports.

Because the issue is intermittent, the conversation is repeated `--runs`
times. Exits non-zero if duplication is detected in any run.

Prerequisites:
    Start the bot server:   python pipecat-ws-server/server.py

Usage:
    python test_duplicate_words.py [--runs N] [--url ws://...] [--verbose]

Detection strategy
------------------
For each bot turn we accumulate the text from every `bot-output` event with
`spoken == True` (the post-TTS text, time-aligned with audio). Two checks
are then run on the assembled string:

  1. Adjacent token duplication: split into whitespace-separated tokens with
     leading/trailing punctuation stripped; flag positions where a non-empty
     token equals the immediately preceding one (case-insensitive). This
     catches patterns like "Welcome Welcome to to the the".

  2. Phrase repetition: split into 3-grams of stripped tokens and flag any
     3-gram that appears more than once. This catches whole-sentence
     duplications like "Please tell me your full name. Please tell me ...".
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import re
import sys
import time
import uuid
from dataclasses import dataclass, field
from typing import Optional

import websockets

# The protobuf module lives next to this script's sibling chat_client/ folder.
_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_HERE, "chat_client"))
import frames_pb2  # noqa: E402  (module path adjusted above)


# Sequence of user inputs reproduced from a real session log
# (samples/python/hosted-agents/bring-your-own/invocations_ws/pipecat-ws-server/chat_client/portal.log).
DEFAULT_INPUTS: list[str] = [
    "order status",
    "hi",
    "hi",
    "hi",
    "hello",
    "can you hear me",
    "tell me a story",
]


RTVI_PROTOCOL_VERSION = "1.2.0"


# ---------------------------------------------------------------------------
# Protobuf helpers (mirror client/web_portal.py so the bot sees an identical
# wire-protocol regardless of whether the request comes from the proxy or
# from this test).
# ---------------------------------------------------------------------------


def _make_rtvi_frame(msg_type: str, data: Optional[dict] = None) -> bytes:
    msg = {
        "id": uuid.uuid4().hex[:8],
        "label": "rtvi-ai",
        "type": msg_type,
        "data": data,
    }
    frame = frames_pb2.Frame()
    frame.message.data = json.dumps(msg)
    return frame.SerializeToString()


def _make_audio_frame(pcm: bytes, sr: int = 16000, ch: int = 1) -> bytes:
    frame = frames_pb2.Frame()
    frame.audio.audio = pcm
    frame.audio.sample_rate = sr
    frame.audio.num_channels = ch
    return frame.SerializeToString()


def _parse_frame(raw: bytes) -> dict:
    frame = frames_pb2.Frame()
    frame.ParseFromString(raw)
    kind = frame.WhichOneof("frame")
    if kind == "message":
        return {"type": "message", "message": json.loads(frame.message.data)}
    if kind == "audio":
        return {"type": "audio"}  # we don't care about audio in this test
    if kind == "text":
        return {"type": "text", "text": frame.text.text}
    if kind == "transcription":
        return {
            "type": "transcription",
            "text": frame.transcription.text,
            "user_id": frame.transcription.user_id,
        }
    return {"type": "unknown"}


# ---------------------------------------------------------------------------
# Duplicate detection
# ---------------------------------------------------------------------------


_TOKEN_TRIM = re.compile(r"^[^\w']+|[^\w']+$")


def _normalize_tokens(text: str) -> list[str]:
    out: list[str] = []
    for raw in text.split():
        token = _TOKEN_TRIM.sub("", raw)
        if token:
            out.append(token.lower())
    return out


def find_adjacent_duplicates(text: str) -> list[tuple[int, str]]:
    """Return (index, token) pairs where token equals the previous token."""
    tokens = _normalize_tokens(text)
    dups: list[tuple[int, str]] = []
    for i in range(1, len(tokens)):
        if tokens[i] == tokens[i - 1]:
            dups.append((i, tokens[i]))
    return dups


def find_repeated_phrases(text: str, n: int = 5) -> dict[str, int]:
    """Return n-grams that appear more than once (count > 1).

    Uses 5-grams (instead of 3) to avoid false positives on legitimate
    natural-language repetition. The duplication bug we are reproducing
    causes long blocks of consecutive sentences to be repeated verbatim,
    which 5-gram matching catches reliably while skipping common phrasing
    like "order status or report a product issue" appearing twice in one
    natural sentence.
    """
    tokens = _normalize_tokens(text)
    if len(tokens) < n:
        return {}
    counts: dict[str, int] = {}
    for i in range(len(tokens) - n + 1):
        ngram = " ".join(tokens[i : i + n])
        counts[ngram] = counts.get(ngram, 0) + 1
    return {k: v for k, v in counts.items() if v > 1}


# ---------------------------------------------------------------------------
# Per-turn capture
# ---------------------------------------------------------------------------


@dataclass
class TurnCapture:
    user_input: str
    spoken_text: str = ""
    raw_chunks: list[str] = field(default_factory=list)
    started_at: float = field(default_factory=time.time)
    ended_at: Optional[float] = None

    def append(self, chunk: str) -> None:
        self.raw_chunks.append(chunk)
        self.spoken_text += chunk

    def summary(self) -> dict:
        adj = find_adjacent_duplicates(self.spoken_text)
        phrases = find_repeated_phrases(self.spoken_text, n=5)
        return {
            "user_input": self.user_input,
            "duration_s": (
                round(self.ended_at - self.started_at, 3)
                if self.ended_at
                else round(time.time() - self.started_at, 3)
            ),
            "spoken_text": self.spoken_text,
            "adjacent_duplicates": adj,
            "repeated_phrases": phrases,
            "has_duplication": bool(adj) or bool(phrases),
        }


# ---------------------------------------------------------------------------
# Conversation driver (talks to the bot server directly via protobuf)
# ---------------------------------------------------------------------------


async def run_conversation(
    url: str,
    inputs: list[str],
    *,
    bot_ready_timeout: float = 30.0,
    initial_settle_secs: float = 4.0,
    inter_turn_secs: float = 3.0,
    final_drain_secs: float = 6.0,
    verbose: bool = False,
) -> list[TurnCapture]:
    """Open one ws session, send each input on a fixed cadence, capture text."""

    captures: list[TurnCapture] = []
    state: dict = {"bot_ready": False, "closed": False}
    current: Optional[TurnCapture] = None

    async with websockets.connect(
        url, max_size=None, open_timeout=30, ping_interval=20, ping_timeout=20
    ) as ws:

        # Send client-ready (RTVI). The server will then emit `bot-ready`.
        await ws.send(
            _make_rtvi_frame(
                "client-ready",
                {
                    "version": RTVI_PROTOCOL_VERSION,
                    "about": {"library": "test_duplicate_words"},
                },
            )
        )

        # Silence sender - keeps VAD/STT alive. The bot's input pipeline
        # expects a continuous audio stream; if we never send audio the
        # agent may not advance properly.
        stop_silence = asyncio.Event()

        async def silence_loop():
            silence = b"\x00" * 640  # 20 ms @ 16 kHz mono 16-bit
            try:
                while not stop_silence.is_set() and not state["closed"]:
                    await ws.send(_make_audio_frame(silence))
                    await asyncio.sleep(0.02)
            except (websockets.exceptions.ConnectionClosed, RuntimeError):
                state["closed"] = True

        async def reader():
            try:
                async for raw in ws:
                    if isinstance(raw, bytes):
                        parsed = _parse_frame(raw)
                        if parsed["type"] != "message":
                            continue
                        rtvi = parsed["message"]
                    else:
                        try:
                            rtvi = json.loads(raw)
                        except Exception:
                            continue

                    rtype = rtvi.get("type")
                    rdata = rtvi.get("data") or {}

                    if rtype == "bot-ready":
                        state["bot_ready"] = True
                    elif rtype == "bot-output":
                        # Only count what was spoken (TTSTextFrame path) to
                        # mirror the UI rendering and avoid double-counting
                        # the pre-TTS AggregatedTextFrame.
                        if rdata.get("spoken") and current is not None:
                            current.append(rdata.get("text", ""))
            except websockets.exceptions.ConnectionClosed:
                pass
            finally:
                state["closed"] = True

        silence_task = asyncio.create_task(silence_loop())
        reader_task = asyncio.create_task(reader())

        try:
            # Wait for bot-ready
            t0 = time.monotonic()
            while not state["bot_ready"] and not state["closed"]:
                if time.monotonic() - t0 > bot_ready_timeout:
                    raise RuntimeError("timed out waiting for bot-ready")
                await asyncio.sleep(0.05)
            if state["closed"]:
                raise RuntimeError("connection closed before bot-ready")

            # Let the unsolicited greeter welcome play out before we start
            # injecting user text.
            await asyncio.sleep(initial_settle_secs)

            for user_text in inputs:
                if state["closed"]:
                    raise RuntimeError(
                        "connection closed before all inputs were sent"
                    )
                # Mark the previous turn as ended so its summary uses the
                # capture window we actually waited.
                if current is not None and current.ended_at is None:
                    current.ended_at = time.time()

                current = TurnCapture(user_input=user_text)
                captures.append(current)

                if verbose:
                    print(f"  -> user: {user_text!r}")
                await ws.send(
                    _make_rtvi_frame(
                        "send-text",
                        {
                            "content": user_text,
                            "options": {
                                "run_immediately": True,
                                "audio_response": True,
                            },
                        },
                    )
                )
                await asyncio.sleep(inter_turn_secs)

            # Drain any final tokens that arrive after the last send.
            await asyncio.sleep(final_drain_secs)
            if current is not None and current.ended_at is None:
                current.ended_at = time.time()
        finally:
            stop_silence.set()
            for t in (silence_task, reader_task):
                t.cancel()
                try:
                    await t
                except (asyncio.CancelledError, Exception):
                    pass

    return captures


# ---------------------------------------------------------------------------
# Reporting
# ---------------------------------------------------------------------------


def _print_capture(idx: int, cap: TurnCapture, verbose: bool) -> bool:
    s = cap.summary()
    flag = " <-- DUPLICATION" if s["has_duplication"] else ""
    print(
        f"  Turn {idx} (user={cap.user_input!r}, dur={s['duration_s']}s):{flag}"
    )
    if verbose or s["has_duplication"]:
        print(f"    spoken: {s['spoken_text']!r}")
    if s["adjacent_duplicates"]:
        sample = ", ".join(t for _, t in s["adjacent_duplicates"][:5])
        print(
            f"    adjacent_duplicates ({len(s['adjacent_duplicates'])}): {sample}"
        )
    if s["repeated_phrases"]:
        items = sorted(s["repeated_phrases"].items(), key=lambda kv: -kv[1])[:5]
        print(
            "    repeated_phrases: "
            + "; ".join(f"{p!r}x{c}" for p, c in items)
        )
    return s["has_duplication"]


async def main_async(args: argparse.Namespace) -> int:
    bad_runs = 0
    for run in range(1, args.runs + 1):
        print(f"\n=== Run {run}/{args.runs} ===")
        try:
            captures = await run_conversation(
                args.url,
                args.inputs,
                inter_turn_secs=args.inter_turn_secs,
                verbose=args.verbose,
            )
        except Exception as e:
            print(f"  [error] conversation failed: {e}")
            bad_runs += 1
            continue

        run_has_dup = False
        for i, cap in enumerate(captures, 1):
            if _print_capture(i, cap, args.verbose):
                run_has_dup = True
        if run_has_dup:
            bad_runs += 1

    print(
        f"\n=== Summary: {bad_runs}/{args.runs} runs contained duplicated "
        f"bot text ==="
    )
    return 1 if bad_runs else 0


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument(
        "--url",
        default="ws://localhost:8088/invocations_ws",
        help="WebSocket URL of the running bot server (server.py).",
    )
    p.add_argument(
        "--runs",
        type=int,
        default=10,
        help="How many full conversations to run (issue is intermittent).",
    )
    p.add_argument(
        "--inputs",
        nargs="*",
        default=DEFAULT_INPUTS,
        help="User text inputs to send (default: replay of portal.log).",
    )
    p.add_argument(
        "--inter-turn-secs",
        type=float,
        default=3.0,
        help="Seconds to wait between successive text inputs (default: 3.0).",
    )
    p.add_argument("--verbose", action="store_true")
    args = p.parse_args()
    return asyncio.run(main_async(args))


if __name__ == "__main__":
    sys.exit(main())
