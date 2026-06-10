# `invocations_ws` voice-agent samples

This folder collects four reference voice agents that expose the
[`invocations_ws`](https://learn.microsoft.com/en-us/azure/ai-foundry/agents)
WebSocket protocol. Each one ships its own browser portal under
`chat_client/` (except `hello-world`, which uses a single-file static
client).

```
invocations_ws/
├── hello-world/          ← Minimal Voice Live agent. Audio over WebSocket.
│   └── chat_client/      ← Single-file browser client for this sample.
├── pipecat-ws-server/    ← Voice agent. Audio over WebSocket.
│   └── chat_client/      ← Browser portal for this sample.
├── pipecat-webrtc/       ← Voice agent. Signaling over WebSocket, media over WebRTC.
│   └── chat_client/      ← Browser portal for this sample.
└── livekit-server/       ← LiveKit-based voice agent. WebSocket carries signaling only.
    └── chat_client/      ← Browser portal for this sample.
```

## Demos

### [`hello-world/`](hello-world/) — Voice Live hello world

The smallest possible `invocations_ws` agent (~250 lines of Python).
Each browser WebSocket connection is bridged to a fresh
[**Azure Voice Live**](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live)
session, which owns STT + LLM + TTS in one managed service. This sample
is the recommended starting point for understanding the
`invocations_ws` contract before moving on to the framework-based
samples.

The browser portal lives at
[`hello-world/chat_client/`](hello-world/chat_client/) (a single
static `index.html`) and defaults to <http://localhost:8080>.

### [`pipecat-ws-server/`](pipecat-ws-server/) — Pipecat over WebSocket

Real-time voice agent built on [pipecat](https://github.com/pipecat-ai/pipecat)
with Azure Fast Transcription (STT) → two Azure OpenAI sub-agents
(greeter + check-order) → Azure TTS. The browser sends mic PCM and
receives bot PCM directly over the `invocations_ws` socket using
pipecat's protobuf framing.

The browser portal lives at
[`pipecat-ws-server/chat_client/`](pipecat-ws-server/chat_client/) and
defaults to <http://localhost:9527>.

### [`pipecat-webrtc/`](pipecat-webrtc/) — Pipecat with WebRTC media

Same multi-agent pipecat bot as above, but the `invocations_ws` socket
is **signaling-only**. Audio media flows browser ↔ agent over a
WebRTC peer connection negotiated through the WebSocket
(`ice_config` / `offer` / `answer` / `ice_candidate`), with TURN
credentials supplied via environment variables.

The browser portal lives at
[`pipecat-webrtc/chat_client/`](pipecat-webrtc/chat_client/) and
defaults to <http://localhost:9528>.

### [`livekit-server/`](livekit-server/) — LiveKit voice agent

Voice agent built on the [LiveKit Agents](https://docs.livekit.io/agents/)
framework with Azure STT/LLM/TTS plugins. The `invocations_ws` socket
returns LiveKit room credentials (`livekit_url`, `token`, `room`,
`identity`); audio media then flows browser ↔ LiveKit ↔ agent
directly, bypassing this server.

The browser portal lives at
[`livekit-server/chat_client/`](livekit-server/chat_client/) and
defaults to <http://localhost:9529>.

Each demo's README under `<sample>/README.md` documents how to run that
specific agent locally and how to deploy it to a Foundry hosted-agent
container with `azd`. Each portal's README under
`<sample>/chat_client/README.md` covers configuring and running the
browser side.


### [`duplex-live-agent/`](duplex-live-agent/) — Voice live foreground and background agents

The sample shows how to build real-time voice agents that maintain two parallel tracks simultaneously with [**Azure Voice Live**](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live):
* Foreground router: Low-latency voice conversation (always responsive, never "freezes")
* Background workers: Autonomous task execution (research, analysis, multi-step operations)

The browser portal lives at
[`duplex-live-agent/chat_client/`](duplex-live-agent/chat_client/) (a single
static `index.html`) and defaults to <http://localhost:8080>.
