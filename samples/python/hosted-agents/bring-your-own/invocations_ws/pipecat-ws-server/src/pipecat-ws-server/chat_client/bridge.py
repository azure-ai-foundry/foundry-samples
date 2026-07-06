# Copyright (c) Microsoft. All rights reserved.

"""PCM <-> protobuf bridge for the pipecat-ws-server browser portal.

The browser speaks a simple wire protocol (raw PCM16 mic + JSON text), but
the bot expects pipecat protobuf ``Frame`` messages. This module wraps mic
chunks into ``AudioRawFrame``s and unwraps incoming frames back into the
browser's simple format. It also pumps silence until the browser starts
sending real audio (so VAD stays alive) and forwards typed text via an
RTVI ``send-text`` message.
"""

from __future__ import annotations

import asyncio
import json
import logging
import struct
import uuid

import websockets
from fastapi import WebSocket, WebSocketDisconnect

from recorder import ConversationRecorder


logger = logging.getLogger("chat-client.bridge")

RTVI_PROTOCOL_VERSION = "1.2.0"
MIC_SR = 16000
MIC_CH = 1


def _make_rtvi_frame(msg_type: str, data=None) -> bytes:
    import frames_pb2
    msg = {
        "id": uuid.uuid4().hex[:8],
        "label": "rtvi-ai",
        "type": msg_type,
        "data": data,
    }
    frame = frames_pb2.Frame()
    frame.message.data = json.dumps(msg)
    return frame.SerializeToString()


def _make_audio_frame(pcm: bytes, sr: int = MIC_SR, ch: int = MIC_CH) -> bytes:
    import frames_pb2
    frame = frames_pb2.Frame()
    frame.audio.audio = pcm
    frame.audio.sample_rate = sr
    frame.audio.num_channels = ch
    return frame.SerializeToString()


def _parse_frame(raw: bytes) -> dict:
    import frames_pb2
    frame = frames_pb2.Frame()
    frame.ParseFromString(raw)
    kind = frame.WhichOneof("frame")
    if kind == "audio":
        return {
            "type": "audio",
            "audio": bytes(frame.audio.audio),
            "sample_rate": frame.audio.sample_rate,
            "num_channels": frame.audio.num_channels,
        }
    if kind == "message":
        return {"type": "message", "message": json.loads(frame.message.data)}
    if kind == "text":
        return {"type": "text", "text": frame.text.text}
    if kind == "transcription":
        return {
            "type": "transcription",
            "text": frame.transcription.text,
            "user_id": frame.transcription.user_id,
        }
    return {"type": "unknown"}


async def run(
    ws: WebSocket,
    upstream,
    recorder: ConversationRecorder | None,
) -> None:
    # RTVI client-ready handshake.
    await upstream.send(
        _make_rtvi_frame(
            "client-ready",
            {"version": RTVI_PROTOCOL_VERSION, "about": {"library": "chat-client"}},
        )
    )

    # Pump silence until the browser starts sending real mic audio (keeps
    # VAD alive).
    got_browser_audio = asyncio.Event()

    async def silence_loop():
        silence = b"\x00" * 640  # 20ms @ 16kHz mono int16
        try:
            while not got_browser_audio.is_set():
                await upstream.send(_make_audio_frame(silence))
                await asyncio.sleep(0.02)
        except asyncio.CancelledError:
            return

    silence_task = asyncio.create_task(silence_loop())

    async def b2u():
        try:
            while True:
                msg = await ws.receive()
                if msg.get("type") == "websocket.disconnect":
                    return
                if msg.get("bytes") is not None:
                    got_browser_audio.set()
                    pcm = msg["bytes"]
                    if recorder is not None:
                        recorder.add_user(pcm, MIC_SR, MIC_CH)
                    await upstream.send(_make_audio_frame(pcm))
                elif msg.get("text") is not None:
                    try:
                        data = json.loads(msg["text"])
                    except json.JSONDecodeError:
                        continue
                    if data.get("type") == "text":
                        content = data.get("content")
                        if not content:
                            logger.warning("b2u: ignoring 'text' frame with empty content")
                            continue
                        await upstream.send(
                            _make_rtvi_frame(
                                "send-text",
                                {
                                    "content": content,
                                    "options": {
                                        "run_immediately": True,
                                        "audio_response": True,
                                    },
                                },
                            )
                        )
        except WebSocketDisconnect:
            return
        except Exception as e:
            logger.error("b2u: %s", e)

    async def u2b():
        try:
            async for raw in upstream:
                if isinstance(raw, bytes):
                    parsed = _parse_frame(raw)
                    if parsed["type"] == "audio":
                        if recorder is not None:
                            recorder.add_bot(
                                parsed["audio"],
                                parsed["sample_rate"],
                                parsed["num_channels"],
                            )
                        # 8-byte header (sr u32LE + num_channels u32LE) + PCM
                        hdr = struct.pack(
                            "<II", parsed["sample_rate"], parsed["num_channels"]
                        )
                        await ws.send_bytes(hdr + parsed["audio"])
                    else:
                        await ws.send_json(parsed)
                else:
                    await ws.send_text(raw)
        except websockets.exceptions.ConnectionClosed:
            return
        except Exception as e:
            logger.error("u2b: %s", e)

    tasks = [asyncio.create_task(b2u()), asyncio.create_task(u2b())]
    try:
        _done, pending = await asyncio.wait(
            tasks, return_when=asyncio.FIRST_COMPLETED
        )
        for t in pending:
            t.cancel()
        for t in pending:
            try:
                await t
            except (asyncio.CancelledError, Exception):
                pass
    finally:
        silence_task.cancel()
        try:
            await silence_task
        except (asyncio.CancelledError, Exception):
            pass
