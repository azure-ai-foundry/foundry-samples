# Browser portal — `livekit-server`

A small FastAPI portal that lets you talk to the `livekit-server` voice
agent from a browser. The WebSocket carries LiveKit signaling only
(`{action: "join"}` → `{type: "config", livekit_url, token, room,
identity}`); audio flows directly browser ↔ LiveKit ↔ agent over
LiveKit's WebRTC stack.

```
browser  ──►  static/index.html  ── /ws/connect ──►  livekit-server
                                  (signaling JSON)     (local or hosted)

browser  ◄══════════════ LiveKit WebRTC (audio + data) ═══════════════►  agent
```

The proxy:

- Picks the upstream URL from `chat_client/.env` — either
  `LIVEKIT_LOCAL_URL` (local mode) or `PROJECT_ENDPOINT` +
  `LIVEKIT_AGENT_NAME` (Foundry-hosted mode).
- In Foundry mode, fetches an Entra Bearer token via
  `az account get-access-token --resource https://ai.azure.com` and
  attaches it as the `Authorization` header.
- Forwards signaling JSON verbatim in both directions.

## Install

```bash
cd samples/python/hosted-agents/bring-your-own/invocations_ws/livekit-server/chat_client

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
python web_portal.py     # http://localhost:9529
```

Open <http://localhost:9529>, click **▶ Start**, allow microphone access, and start speaking.

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
    └── app.js            # livekit client logic (signaling + LiveKit SDK)
```
