# Copyright (c) Microsoft. All rights reserved.

"""
Browser portal for the ``pipecat-webrtc`` sample.

  Browser  <->  /ws/connect  <->  upstream invocations_ws (signaling)
                (single page)     (auth, hosted or local)
                  +
                browser  <-- WebRTC peer connection -->  bot
                                (audio bypasses the proxy)

The browser exchanges WebRTC signaling JSON (ice_config / offer / answer /
ice_candidate / disconnect) with the bot through this proxy. Audio media
itself flows browser <-> bot directly over the WebRTC peer connection and
never passes through the proxy, so no recording or transcoding happens here.
"""

import asyncio
import logging
import os
from pathlib import Path

import uvicorn
import websockets
from dotenv import load_dotenv
from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles

import upstream


_HERE = Path(__file__).resolve().parent
load_dotenv(_HERE / ".env", override=True)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(name)s %(levelname)s: %(message)s",
)
logger = logging.getLogger("chat-client")

DEFAULT_PORT = int(os.environ.get("PORTAL_PORT", "9528"))


app = FastAPI()
app.mount("/static", StaticFiles(directory=str(_HERE / "static")), name="static")


@app.get("/")
async def root():
    return FileResponse(str(_HERE / "static" / "index.html"))


@app.get("/health")
async def health():
    return {"status": "ok"}


async def _passthrough(ws: WebSocket, upstream_ws) -> None:
    """Forward binary + text frames verbatim in both directions."""
    async def b2u():
        try:
            while True:
                msg = await ws.receive()
                if msg.get("type") == "websocket.disconnect":
                    return
                if msg.get("bytes") is not None:
                    await upstream_ws.send(msg["bytes"])
                elif msg.get("text") is not None:
                    await upstream_ws.send(msg["text"])
        except WebSocketDisconnect:
            return
        except Exception as e:
            logger.error("b2u: %s", e)

    async def u2b():
        try:
            async for raw in upstream_ws:
                if isinstance(raw, bytes):
                    await ws.send_bytes(raw)
                else:
                    await ws.send_text(raw)
        except websockets.exceptions.ConnectionClosed:
            return
        except Exception as e:
            logger.error("u2b: %s", e)

    tasks = [asyncio.create_task(b2u()), asyncio.create_task(u2b())]
    _done, pending = await asyncio.wait(tasks, return_when=asyncio.FIRST_COMPLETED)
    for t in pending:
        t.cancel()
    for t in pending:
        try:
            await t
        except (asyncio.CancelledError, Exception):
            pass


@app.websocket("/ws/connect")
async def ws_proxy(ws: WebSocket):
    await ws.accept()
    try:
        upstream_url, session_id, hdrs = upstream.resolve()
    except Exception as e:
        logger.error("config error: %s", e)
        try:
            await ws.send_json({"type": "error", "message": str(e)})
        finally:
            await ws.close()
        return

    await ws.send_json({"type": "session", "session_id": session_id})

    try:
        async with websockets.connect(
            upstream_url,
            additional_headers=hdrs,
            max_size=4 * 1024 * 1024,
            open_timeout=30,
            ping_interval=20,
            ping_timeout=20,
        ) as up_ws:
            logger.info("upstream connected")
            await _passthrough(ws, up_ws)
    except Exception as e:
        logger.error("proxy error: %s", e)
        try:
            await ws.send_json({"type": "error", "message": str(e)})
        except Exception:
            pass
    finally:
        try:
            await ws.close()
        except Exception:
            pass


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser(description="pipecat-webrtc browser portal.")
    parser.add_argument(
        "--port", type=int, default=DEFAULT_PORT,
        help=f"Listening port (default: {DEFAULT_PORT}, env: PORTAL_PORT).",
    )
    parser.add_argument(
        "--host", default=os.environ.get("PORTAL_HOST", "0.0.0.0"),
        help="Listening host (default: 0.0.0.0, env: PORTAL_HOST).",
    )
    args = parser.parse_args()
    uvicorn.run(app, host=args.host, port=args.port)


if __name__ == "__main__":
    main()
