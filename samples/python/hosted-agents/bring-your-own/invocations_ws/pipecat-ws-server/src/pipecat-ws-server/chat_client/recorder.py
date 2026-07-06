# Copyright (c) Microsoft. All rights reserved.

"""Conversation recorder for the pipecat-ws-server web portal.

Captures the user's mic audio (browser -> proxy -> upstream) and the bot's
TTS audio (upstream -> proxy -> browser) and writes them to a single
stereo WAV when the WebSocket session ends.

Channel mapping:
    left  -> bot
    right -> user

Pipeline per chunk:
  1. Reduce to mono int16 (averages channels if needed).
  2. Linear-interpolate resample to ``TARGET_SR`` (16 kHz).
  3. Append to that track. If wall-clock has advanced past the number of
     samples already buffered for this track (i.e. there was a gap with no
     audio), pad the front of this chunk with silence so the gap is
     preserved and the two tracks stay in sync.

This append-with-silence-padding approach is robust against bursty input
(many chunks arriving in a single event-loop tick), unlike a naive
``out[offset:offset+len] = chunk`` which overwrites earlier samples and
produces audible glitches.

Output location: ``$RECORDINGS_DIR`` (default: ``./recordings/`` relative
to this file). One WAV per session, named ``<timestamp>-<session_id>.wav``.
"""
from __future__ import annotations

import array
import logging
import os
import time
import wave
from datetime import datetime
from pathlib import Path

logger = logging.getLogger("recorder")

TARGET_SR = 16000


def _to_mono_int16(pcm_bytes: bytes, num_channels: int) -> array.array:
    s = array.array("h")
    s.frombytes(pcm_bytes)
    if num_channels <= 1:
        return s
    n = len(s) // num_channels
    out = array.array("h", [0] * n)
    for i in range(n):
        acc = 0
        for c in range(num_channels):
            acc += s[i * num_channels + c]
        out[i] = max(-32768, min(32767, acc // num_channels))
    return out


def _resample_int16(samples: array.array, src_sr: int, dst_sr: int) -> array.array:
    if src_sr == dst_sr or len(samples) == 0:
        return samples
    ratio = dst_sr / src_sr
    n_out = int(len(samples) * ratio)
    if n_out == 0:
        return array.array("h")
    out = array.array("h", [0] * n_out)
    last = len(samples) - 1
    for i in range(n_out):
        src_idx = i / ratio
        i0 = int(src_idx)
        i1 = i0 + 1 if i0 < last else last
        frac = src_idx - i0
        v = int(samples[i0] * (1.0 - frac) + samples[i1] * frac)
        if v > 32767:
            v = 32767
        elif v < -32768:
            v = -32768
        out[i] = v
    return out


# Threshold for inserting silence padding. Pipecat audio frames typically
# arrive every 10-40 ms; tiny scheduling jitter shouldn't cause a gap.
_GAP_PAD_THRESHOLD_SAMPLES = TARGET_SR // 50  # 20 ms


class ConversationRecorder:
    """Buffers user + bot PCM and writes a stereo WAV on close."""

    def __init__(self, target_sr: int = TARGET_SR) -> None:
        self.target_sr = target_sr
        self.start_monotonic: float | None = None
        self._user: array.array = array.array("h")
        self._bot: array.array = array.array("h")

    def add_user(
        self,
        pcm_bytes: bytes,
        sample_rate: int = 16000,
        num_channels: int = 1,
    ) -> None:
        self._append(self._user, pcm_bytes, sample_rate, num_channels)

    def add_bot(
        self,
        pcm_bytes: bytes,
        sample_rate: int,
        num_channels: int,
    ) -> None:
        self._append(self._bot, pcm_bytes, sample_rate, num_channels)

    def _append(self, dest: array.array, pcm_bytes, sample_rate, num_channels):
        if not pcm_bytes:
            return
        now = time.monotonic()
        if self.start_monotonic is None:
            self.start_monotonic = now
        mono = _to_mono_int16(pcm_bytes, num_channels or 1)
        mono = _resample_int16(mono, sample_rate or self.target_sr, self.target_sr)
        if not mono:
            return
        # Pad with silence if wall-clock has advanced past what's been
        # written for this track, so gaps are preserved.
        wall_samples = int((now - self.start_monotonic) * self.target_sr)
        gap = wall_samples - len(dest)
        if gap > _GAP_PAD_THRESHOLD_SAMPLES:
            dest.extend([0] * gap)
        dest.extend(mono)

    def save(self, path: str | os.PathLike) -> str | None:
        if not self._user and not self._bot:
            logger.info("recorder: nothing to save")
            return None
        bot = self._bot
        user = self._user
        n = max(len(bot), len(user))
        if n == 0:
            return None
        if len(bot) < n:
            bot.extend([0] * (n - len(bot)))
        if len(user) < n:
            user.extend([0] * (n - len(user)))
        # Interleave: left = bot, right = user
        stereo = array.array("h", [0] * (n * 2))
        for i in range(n):
            stereo[2 * i] = bot[i]
            stereo[2 * i + 1] = user[i]
        out_path = Path(path)
        out_path.parent.mkdir(parents=True, exist_ok=True)
        with wave.open(str(out_path), "wb") as wf:
            wf.setnchannels(2)
            wf.setsampwidth(2)
            wf.setframerate(self.target_sr)
            wf.writeframes(stereo.tobytes())
        logger.info(
            "recorder: wrote %s (%.1fs, bot=%d samples, user=%d samples)",
            out_path,
            n / self.target_sr,
            len(bot),
            len(user),
        )
        return str(out_path)


def default_recording_path(session_id: str = "") -> Path:
    """Return a default path under ``RECORDINGS_DIR`` (or client/recordings/)."""
    base = Path(os.environ.get("RECORDINGS_DIR", "recordings"))
    if not base.is_absolute():
        base = Path(__file__).resolve().parent / base
    sid = session_id or "session"
    safe = "".join(c if c.isalnum() or c in "-_" else "_" for c in sid)
    ts = datetime.now().strftime("%Y%m%d-%H%M%S")
    return base / f"{ts}-{safe}.wav"
