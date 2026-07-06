// LiveKit (Azure) client.
// Browser <-> /ws/connect <-> portal <-> livekit-server signaling.
// The signaling WS hands back {livekit_url, token, room, identity}; the
// browser then uses the LiveKit client SDK to join the room directly,
// and audio flows browser <-> LiveKit server <-> agent.

import {
    Room,
    RoomEvent,
    Track,
    createLocalAudioTrack,
} from "https://esm.sh/livekit-client@2.5.10";

export default async function init(shell) {
    shell.title("🎙️ LiveKit (Azure)");
    shell.setTextPlaceholder("Type a message to the bot...");

    let ws = null;
    let room = null;
    let micTrack = null;

    shell.onStart(async () => {
        const proto = location.protocol === "https:" ? "wss:" : "ws:";
        const url = `${proto}//${location.host}/ws/connect`;
        shell.log("Connecting signaling: " + url);

        ws = new WebSocket(url);

        const config = await new Promise((resolve, reject) => {
            ws.onopen = () => {
                shell.log("Signaling WebSocket connected", "event");
                try { ws.send(JSON.stringify({ action: "join" })); } catch {}
            };
            ws.onerror = () => reject(new Error("signaling ws failed"));
            ws.onclose = () => {
                shell.log("Signaling WebSocket closed", "event");
            };
            ws.onmessage = (ev) => {
                let msg;
                try { msg = JSON.parse(ev.data); } catch {
                    shell.log("non-json signaling: " + ev.data);
                    return;
                }
                if (msg.type === "session") {
                    const sid = msg.session_id || "(local)";
                    shell.setSessionId(sid);
                    shell.log("Session: " + sid, "event");
                    return;
                }
                if (msg.type === "error") {
                    reject(new Error(msg.message || "signaling error"));
                    return;
                }
                if (msg.type === "config") {
                    resolve(msg);
                    return;
                }
                shell.log("signaling msg: " + ev.data);
            };
        });

        shell.log(`Got LiveKit config (room=${config.room}, identity=${config.identity})`, "event");
        shell.log(`Connecting to LiveKit at ${config.livekit_url}`);

        room = new Room({
            adaptiveStream: true,
            dynacast: true,
        });

        room.on(RoomEvent.Connected, () => {
            shell.log("Joined LiveKit room", "event");
            shell.setStatus("Connected — speak now!");
        });
        room.on(RoomEvent.Disconnected, (reason) => {
            shell.log("Left LiveKit room: " + (reason ?? ""), "event");
        });
        room.on(RoomEvent.ParticipantConnected, (p) => {
            shell.log("Participant joined: " + p.identity, "event");
        });
        room.on(RoomEvent.ParticipantDisconnected, (p) => {
            shell.log("Participant left: " + p.identity, "event");
        });
        room.on(RoomEvent.TrackSubscribed, (track, _publication, participant) => {
            if (track.kind === Track.Kind.Audio) {
                shell.log("Subscribed to bot audio: " + participant.identity, "event");
                const el = track.attach();
                el.autoplay = true;
                el.style.display = "none";
                document.body.appendChild(el);
            }
        });
        room.on(RoomEvent.TrackUnsubscribed, (track) => {
            if (track.kind === Track.Kind.Audio) {
                track.detach().forEach((el) => el.remove());
            }
        });
        room.on(RoomEvent.DataReceived, (payload, participant, _kind, topic) => {
            const text = (() => {
                try { return new TextDecoder().decode(payload); } catch { return ""; }
            })();
            if (!text) return;

            // Agent-side AgentSession events (see
            // https://docs.livekit.io/reference/agents/events/) are
            // published on the "agent-events" topic by agent.py. Render
            // each event with a friendly label; everything else falls
            // through to the generic data log.
            if (topic === "agent-events") {
                let evt;
                try { evt = JSON.parse(text); } catch {
                    shell.log("agent-events (non-json): " + text);
                    return;
                }
                switch (evt.type) {
                    case "agent_state_changed":
                        shell.log(
                            `agent_state: ${evt.old} → ${evt.new}` +
                            (evt.ttfa_seconds != null ? `  (TTFA ${evt.ttfa_seconds}s)` : ""),
                            "event",
                        );
                        break;
                    case "user_state_changed":
                        shell.log(`user_state: ${evt.old} → ${evt.new}`, "event");
                        break;
                    case "ttfa":
                        shell.log(`⏱️  TTFA (time to first audio): ${evt.latency_seconds}s`, "event");
                        shell.setStatus(`TTFA: ${evt.latency_seconds}s`);
                        break;
                    case "conversation_item_added":
                        // Skip: the transcription stream already renders
                        // user and bot text in the chat pane, so logging
                        // this event would duplicate every utterance.
                        break;
                    case "function_tools_executed":
                        shell.log(
                            `function_tools_executed: ${evt.tools.join(", ")}` +
                            (evt.has_agent_handoff ? "  (handoff)" : ""),
                            "event",
                        );
                        break;
                    case "user_input_transcribed":
                        if (evt.is_final) {
                            shell.log(`user_input_transcribed (final): ${evt.transcript}`, "event");
                        }
                        break;
                    case "speech_created":
                        shell.log(`speech_created: source=${evt.source} user_initiated=${evt.user_initiated}`, "event");
                        break;
                    case "stt_metrics":
                        shell.log(
                            `📝 STT: duration=${evt.duration}s audio=${evt.audio_duration}s streamed=${evt.streamed}`,
                            "event",
                        );
                        break;
                    case "llm_metrics":
                        shell.log(
                            `🧠 LLM: ttft=${evt.ttft}s duration=${evt.duration}s ` +
                            `tokens=${evt.prompt_tokens}→${evt.completion_tokens} (${evt.tokens_per_second} tok/s)`,
                            "event",
                        );
                        break;
                    case "tts_metrics":
                        shell.log(
                            `🔊 TTS: ttfb=${evt.ttfb}s duration=${evt.duration}s audio=${evt.audio_duration}s streamed=${evt.streamed}`,
                            "event",
                        );
                        break;
                    case "eou_metrics":
                        shell.log(
                            `🎤 EOU: end_of_utterance_delay=${evt.end_of_utterance_delay}s transcription_delay=${evt.transcription_delay}s`,
                            "event",
                        );
                        break;
                    case "close":
                        shell.log(
                            `close: reason=${evt.reason}` + (evt.error ? `  error=${evt.error}` : ""),
                            "event",
                        );
                        break;
                    default:
                        shell.log("agent-events: " + text, "event");
                }
                return;
            }

            shell.log(`data from ${participant?.identity ?? "?"}: ${text}`);
        });
        // LiveKit Agents emits transcription events on text streams.
        // Each segment carries the CUMULATIVE text so far (not a delta),
        // so we track what we've already shown per segment id and only
        // forward the new suffix to the shell.
        const shownBySegment = new Map();
        room.on(RoomEvent.TranscriptionReceived, (segments, participant) => {
            for (const seg of segments) {
                if (!seg.text) continue;
                const isAgent = participant?.identity && participant.identity !== config.identity;
                const key = `${participant?.identity ?? "?"}::${seg.id ?? ""}`;
                const shown = shownBySegment.get(key) ?? "";
                const delta = seg.text.startsWith(shown)
                    ? seg.text.slice(shown.length)
                    : seg.text; // text changed unexpectedly — fall back to full
                shownBySegment.set(key, seg.text);

                if (isAgent) {
                    if (delta) shell.appendBotText(delta);
                    if (seg.final) {
                        shell.finalizeBotText();
                        shownBySegment.delete(key);
                    }
                } else {
                    if (seg.final) {
                        shell.commitUserFinal(seg.text);
                        shownBySegment.delete(key);
                    } else {
                        shell.appendUserPartial(seg.text);
                    }
                }
            }
        });

        await room.connect(config.livekit_url, config.token);

        shell.log("Requesting microphone access...");
        micTrack = await createLocalAudioTrack();
        await room.localParticipant.publishTrack(micTrack);
        shell.log("Published microphone track", "event");
    });

    shell.onSendText(async (text) => {
        if (!room || !text) return;
        try {
            const data = new TextEncoder().encode(
                JSON.stringify({ type: "user-text", text }),
            );
            await room.localParticipant.publishData(data, { reliable: true });
            shell.log("Sent text via data channel: " + text);
        } catch (e) {
            shell.log("publishData failed: " + e.message, "error");
        }
    });

    shell.onStop(async () => {
        try { ws && ws.close(); } catch {}
        ws = null;
        if (micTrack) {
            try { micTrack.stop(); } catch {}
            micTrack = null;
        }
        if (room) {
            try { await room.disconnect(); } catch {}
            room = null;
        }
    });
}
