# Browser portal — `pipecat-webrtc`

A small FastAPI portal that lets you talk to the `pipecat-webrtc` voice
agent from a browser. The WebSocket carries WebRTC signaling only; audio
flows directly browser ↔ bot over the WebRTC peer connection.

```
browser  ──►  static/index.html  ── /ws/connect ──►  pipecat-webrtc bot
                                  (signaling JSON)     (local or hosted)

browser  ◄══════════ WebRTC peer connection (RTP audio + data channel) ═════════►  bot
```

The proxy:

- Picks the upstream URL from `chat_client/.env` — either
  `PIPECAT_WEBRTC_LOCAL_URL` (local mode) or `PROJECT_ENDPOINT` +
  `PIPECAT_WEBRTC_AGENT_NAME` (Foundry-hosted mode).
- In Foundry mode, fetches an Entra Bearer token via
  `az account get-access-token --resource https://ai.azure.com` and
  attaches it + the `Foundry-Features: HostedAgents=V1Preview` header.
- Forwards signaling JSON verbatim in both directions.

## Install

```bash
cd samples/python/hosted-agents/bring-your-own/invocations_ws/pipecat-webrtc/chat_client

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
python web_portal.py     # http://localhost:9528
```

Open <http://localhost:9528>, click **▶ Start**, allow microphone access, and start speaking.

> Microphone access requires a secure context. `http://localhost` counts
> as secure. For LAN testing, terminate TLS in front of the portal
> (e.g. `caddy`, `mkcert`) or use `ngrok`.

## Layout

```
chat_client/
├── web_portal.py         # FastAPI portal (signaling pass-through)
├── upstream.py           # upstream URL + Entra auth resolution
├── requirements.txt
├── .env.example
└── static/
    ├── index.html        # entry page
    ├── shell.css
    ├── shell.js          # UI shell (log panel, controls, toggle, remote audio)
    └── app.js            # pipecat-webrtc client logic (signaling + PC)
```
