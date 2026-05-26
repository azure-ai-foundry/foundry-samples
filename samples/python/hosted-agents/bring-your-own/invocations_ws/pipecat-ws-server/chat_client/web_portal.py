# Copyright (c) Microsoft. All rights reserved.

"""
Browser portal for the ``pipecat-ws-server`` sample.

  Browser  <->  /ws/connect  <->  upstream invocations_ws
                (single page)     (auth, hosted or local)

The wire format on the upstream side is pipecat's protobuf ``Frame``;
``bridge.py`` transcodes between it and the browser's simple PCM/JSON
protocol so the page stays protocol-agnostic.
"""

import logging
import os
from pathlib import Path

import uvicorn
import websockets
from dotenv import load_dotenv
from fastapi import FastAPI, WebSocket
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles

import bridge
import upstream
from recorder import ConversationRecorder, default_recording_path


_HERE = Path(__file__).resolve().parent
load_dotenv(_HERE / ".env", override=True)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(name)s %(levelname)s: %(message)s",
)
logger = logging.getLogger("chat-client")

DEFAULT_PORT = int(os.environ.get("PORTAL_PORT", "9527"))


def _parse_bool(v: str) -> bool | None:
    s = (v or "").strip().lower()
    if s in ("1", "true", "on", "yes", "y"):
        return True
    if s in ("0", "false", "off", "no", "n"):
        return False
    return None


app = FastAPI()
app.mount("/static", StaticFiles(directory=str(_HERE / "static")), name="static")


@app.get("/")
async def root():
    return FileResponse(str(_HERE / "static" / "index.html"))


@app.get("/health")
async def health():
    return {"status": "ok"}


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

    record = _parse_bool(os.environ.get("RECORD_CONVERSATION", "true")) is not False
    recorder: ConversationRecorder | None = ConversationRecorder() if record else None

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
            await bridge.run(ws, up_ws, recorder)
    except Exception as e:
        logger.error("proxy error: %s", e)
        try:
            await ws.send_json({"type": "error", "message": str(e)})
        except Exception:
            pass
    finally:
        if recorder is not None:
            try:
                out = recorder.save(default_recording_path(session_id or "pipecat_websocket"))
                if out:
                    logger.info("saved recording: %s", out)
            except Exception as e:
                logger.warning("failed to save recording: %s", e)
        try:
            await ws.close()
        except Exception:
            pass


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser(description="pipecat-ws-server browser portal.")
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
