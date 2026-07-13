# Browser portal — `pipecat-ws-server`

A small FastAPI portal that lets you talk to the `pipecat-ws-server`
voice agent from a browser:

```
browser  ──►  static/index.html  ── /ws/connect ──►  pipecat-ws-server
                                                        (local or hosted)
```

The proxy:

- Picks the upstream URL from `chat_client/.env` — either
  `PIPECAT_WEBSOCKET_LOCAL_URL` (local mode) or `PROJECT_ENDPOINT` +
  `PIPECAT_WEBSOCKET_AGENT_NAME` (Foundry-hosted mode).
- In Foundry mode, fetches an Entra Bearer token via
  `az account get-access-token --resource https://ai.azure.com` and
  attaches it as the `Authorization` header.
- Transcodes between the browser's simple PCM/JSON wire format and
  pipecat's protobuf framing so the page stays protocol-agnostic.

## Install

```bash
cd samples/python/hosted-agents/bring-your-own/invocations_ws/pipecat-ws-server/chat_client

# Create or reuse a venv.
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
```

## Configure

```bash
cp .env.example .env
# Edit .env (see comments inside)
```

## Run

```bash
az login                 # only needed for Foundry mode
python web_portal.py     # http://localhost:9527
```

Open <http://localhost:9527>, click **▶ Start**, allow microphone access, and start speaking.

## Layout

```
chat_client/
├── web_portal.py         # FastAPI portal (single mode)
├── upstream.py           # upstream URL + Entra auth resolution
├── bridge.py             # PCM <-> pipecat protobuf transcoding
├── frames.proto          # pipecat protobuf schema
├── frames_pb2.py         #   generated bindings
├── recorder.py           # optional stereo conversation recorder
├── requirements.txt
├── .env.example
└── static/
    ├── index.html        # entry page
    ├── shell.css
    ├── shell.js          # generic UI shell (mic, playback, log panel)
    ├── audio-processor.js
    └── app.js            # pipecat-websocket client logic
```
