// Pipecat WebSocket client.
// Browser <-> /ws/connect <-> portal <-> pipecat WS bot.
// Mic: 16 kHz mono PCM16. Bot audio: PCM16 with an 8-byte header
// (sample_rate u32LE + num_channels u32LE) prefixed by the proxy.
// Text frames: portal forwards parsed pipecat frames as JSON (see _parse_frame).

const SR = 16000;

export default async function init(shell) {
    shell.title("🎙️ Pipecat WebSocket");
    shell.setTextPlaceholder("Type a message to the bot...");

    let ws = null;

    function handleRtvi(rtvi) {
        const t = rtvi.type;
        const d = rtvi.data || {};
        switch (t) {
            case "bot-ready":
                shell.log("Bot ready!", "event");
                shell.setStatus("Connected — Listening");
                break;
            case "bot-output":
                // The TTS service emits each token TWICE - once with spoken=false
                // (pre-TTS aggregated text) and once with spoken=true (post-TTS).
                // Render only the spoken copy.
                if (d.text && d.spoken) shell.appendBotText(d.text);
                break;
            case "bot-llm-stopped":
                shell.finalizeBotText();
                break;
            case "user-llm-text":
                if (d.text) shell.commitUserFinal(d.text);
                break;
            case "user-transcription":
                if (d.text) {
                    if (d.final) shell.commitUserFinal(d.text);
                    else shell.appendUserPartial(d.text);
                }
                break;
            case "bot-started-speaking":  shell.log("Bot started speaking", "event"); break;
            case "bot-stopped-speaking":  shell.log("Bot stopped speaking", "event"); shell.finalizeBotText(); break;
            case "user-started-speaking": shell.log("User started speaking", "event"); shell.resetUserBubble(); break;
            case "user-stopped-speaking": shell.log("User stopped speaking", "event"); break;
            case "server-message": {
                const sm = d;
                if (sm.type === "turn-started") shell.log(`Turn ${sm.turn_count} started`, "event");
                else if (sm.type === "turn-ended") {
                    shell.log(`Turn ${sm.turn_count} ${sm.was_interrupted ? "interrupted" : "completed"} after ${sm.duration}s`, "event");
                    shell.finalizeBotText();
                } else if (sm.type === "ttfa") shell.log(`TTFA: ${sm.latency_seconds}s`, "event");
                else shell.log("Server: " + JSON.stringify(sm));
                break;
            }
            case "metrics": {
                const has = Object.values(d).some((v) => v != null && v !== "");
                if (has) shell.log("Metrics: " + JSON.stringify(d));
                break;
            }
            case "error":
                shell.log("Error: " + (d.message || JSON.stringify(d)), "error");
                break;
            // Quiet, expected events.
            case "bot-llm-text": case "bot-llm-started": case "bot-tts-started":
            case "bot-tts-stopped": case "bot-tts-text": case "bot-transcription":
            case "client-ready":
                break;
            default:
                shell.log("Event: " + t + " | " + JSON.stringify(d));
        }
    }

    function handleProxyMessage(msg) {
        if (msg.type === "session") {
            const sid = msg.session_id || "(local)";
            shell.setSessionId(sid);
            shell.log("Session: " + sid, "event");
        } else if (msg.type === "message" && msg.message) {
            handleRtvi(msg.message);
        } else if (msg.type === "transcription") {
            shell.log("User transcript: " + msg.text, "user-text");
        } else if (msg.type === "text") {
            shell.log("Text: " + msg.text);
        } else if (msg.type === "error") {
            shell.log("Proxy error: " + msg.message, "error");
        }
    }

    shell.onStart(async () => {
        const proto = location.protocol === "https:" ? "wss:" : "ws:";
        const url = `${proto}//${location.host}/ws/connect`;
        shell.log("Connecting WebSocket: " + url);
        ws = new WebSocket(url);
        ws.binaryType = "arraybuffer";

        await new Promise((resolve, reject) => {
            ws.onopen = () => { shell.log("WebSocket connected", "event"); resolve(); };
            ws.onerror = () => reject(new Error("WebSocket error"));
        });

        ws.onclose = () => { shell.log("WebSocket closed", "event"); ws = null; };
        ws.onmessage = (ev) => {
            if (ev.data instanceof ArrayBuffer) {
                if (ev.data.byteLength <= 8) return;
                const view = new DataView(ev.data);
                const sr = view.getUint32(0, true);
                const ch = view.getUint32(4, true);
                shell.playPcm(new Uint8Array(ev.data, 8), { sampleRate: sr, channels: ch });
            } else {
                try { handleProxyMessage(JSON.parse(ev.data)); }
                catch { shell.log("Unknown WS message: " + ev.data, "error"); }
            }
        };

        await shell.startMic({
            sampleRate: SR,
            chunkSamples: 1600,  // 100ms
            onChunk: (buf) => {
                if (ws && ws.readyState === WebSocket.OPEN) ws.send(buf);
            },
        });
    });

    shell.onSendText(async (text) => {
        if (ws && ws.readyState === WebSocket.OPEN) {
            ws.send(JSON.stringify({ type: "text", content: text }));
        }
    });

    shell.onStop(async () => {
        if (ws) { try { ws.close(); } catch {} ws = null; }
    });
}
