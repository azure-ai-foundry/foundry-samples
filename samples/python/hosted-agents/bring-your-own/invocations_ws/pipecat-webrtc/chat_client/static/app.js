// Pipecat WebRTC client.
// Browser <-> /ws/connect <-> portal <-> pipecat WebRTC bot.
// The WS is signaling only (ice_config / offer / answer / ice_candidate);
// audio + RTVI app messages flow over the peer connection / data channel.

export default async function init(shell) {
    shell.title("🎙️ Pipecat WebRTC");
    shell.setTextPlaceholder("Type a message to the bot...");

    let ws = null, pc = null, dataChannel = null;
    const pendingReplies = [];

    // Optional: force TURN-relay-only ICE.
    let turnOnly = false;
    shell.addToggle({
        id: "turnOnly", label: "Force TURN relay only", default: false,
        onChange: (v) => { turnOnly = v; },
    });

    function awaitReply() {
        return new Promise((resolve, reject) => pendingReplies.push({ resolve, reject }));
    }
    function send(action, data = {}) {
        if (!ws || ws.readyState !== WebSocket.OPEN) return Promise.reject(new Error("ws closed"));
        const p = awaitReply();
        ws.send(JSON.stringify({ action, data }));
        return p;
    }

    function handleApp(raw) {
        if (typeof raw === "string" && raw.startsWith("ping")) return;
        let msg; try { msg = JSON.parse(raw); } catch { return; }
        const type = msg?.type;
        const d = msg?.data ?? {};
        switch (type) {
            case "user-started-speaking":
                shell.log("User started speaking", "event");
                shell.resetUserBubble();
                break;
            case "user-stopped-speaking":
                shell.log("User stopped speaking", "event");
                break;
            case "user-transcription":
                if (d.text) {
                    if (d.final) shell.commitUserFinal(d.text);
                    else shell.appendUserPartial(d.text);
                }
                break;
            case "bot-started-speaking":
                shell.log("Bot started speaking", "event");
                shell.finalizeBotText();
                break;
            case "bot-output":
                if (d.spoken && d.text) shell.appendBotText(d.text);
                break;
            case "bot-stopped-speaking":
                shell.log("Bot stopped speaking", "event");
                shell.finalizeBotText();
                break;
            case "server-message": {
                const sm = d ?? {};
                if (sm.type === "turn-started") shell.log(`Turn ${sm.turn_count} started`, "event");
                else if (sm.type === "turn-ended") {
                    shell.log(`Turn ${sm.turn_count} ${sm.was_interrupted ? "interrupted" : "ended"} after ${sm.duration}s`, "event");
                    shell.finalizeBotText();
                } else if (sm.type === "ttfa") shell.log(`TTFA: ${sm.latency_seconds}s`, "event");
                else shell.log("server-message: " + JSON.stringify(sm));
                break;
            }
            case "bot-llm-started": case "bot-llm-stopped": case "bot-tts-started":
            case "bot-tts-stopped": case "bot-tts-text": case "bot-llm-text":
            case "bot-transcription": case "metrics": case "client-ready":
            case "bot-ready": case "signalling":
                break;
            default:
                shell.log("rtvi: " + (type ?? "(no type)") + " " + JSON.stringify(d));
        }
    }

    shell.onStart(async () => {
        const proto = location.protocol === "https:" ? "wss:" : "ws:";
        const url = `${proto}//${location.host}/ws/connect`;
        shell.log("Connecting signaling: " + url);
        ws = new WebSocket(url);
        ws.binaryType = "arraybuffer";

        await new Promise((resolve, reject) => {
            ws.onopen = () => { shell.log("Signaling WebSocket connected", "event"); resolve(); };
            ws.onerror = () => reject(new Error("signaling ws failed"));
        });

        ws.onclose = () => {
            shell.log("Signaling WebSocket closed", "event");
            ws = null;
            while (pendingReplies.length) pendingReplies.shift().reject(new Error("ws closed"));
        };
        ws.onmessage = (ev) => {
            let msg; try { msg = JSON.parse(ev.data); } catch { shell.log("non-json: " + ev.data); return; }
            if (msg.type === "session") {
                const sid = msg.session_id || "(local)";
                shell.setSessionId(sid);
                shell.log("Session: " + sid, "event");
                return;
            }
            if (msg.type === "error") { shell.log("Proxy error: " + (msg.message || JSON.stringify(msg)), "error"); return; }
            if (msg.type === "closed") { shell.log("Server signaled connection closed", "event"); return; }
            const next = pendingReplies.shift();
            if (next) next.resolve(msg); else shell.log("unsolicited: " + ev.data);
        };

        shell.log("Fetching ICE config...");
        const iceCfg = await send("ice_config");
        shell.log(`Received ${iceCfg.iceServers.length} ICE server group(s)`);

        const cfg = { iceServers: iceCfg.iceServers };
        if (turnOnly) { cfg.iceTransportPolicy = "relay"; shell.log("Forcing TURN relay only"); }
        pc = new RTCPeerConnection(cfg);

        pc.ontrack = (e) => {
            shell.log("Received remote audio track", "event");
            shell.remoteAudioEl.style.display = "";
            shell.remoteAudioEl.srcObject = e.streams[0];
        };
        pc.oniceconnectionstatechange = () => shell.log("ICE connection state: " + pc.iceConnectionState);
        pc.onicegatheringstatechange  = () => shell.log("ICE gathering: " + pc.iceGatheringState);
        pc.onicecandidateerror = (e) => shell.log(`ICE error: ${e.errorCode} - ${e.errorText}`, "error");
        pc.onconnectionstatechange = () => {
            shell.log("PC state: " + pc.connectionState, "event");
            if (pc.connectionState === "connected") shell.setStatus("Connected — speak now!");
        };
        pc.onicecandidate = async (e) => {
            if (!e.candidate) { shell.log("Local ICE gathering complete"); return; }
            try {
                await send("ice_candidate", {
                    candidate: e.candidate.candidate,
                    sdp_mid: e.candidate.sdpMid,
                    sdp_mline_index: e.candidate.sdpMLineIndex,
                });
            } catch (err) { shell.log("ice_candidate send failed: " + err.message, "error"); }
        };

        // Pipecat's SmallWebRTCConnection expects the *client* to create
        // the data channel and uses it for RTVI/app messages.
        dataChannel = pc.createDataChannel("messaging");
        dataChannel.onopen = () => shell.log("Data channel open", "event");
        dataChannel.onmessage = (ev) => handleApp(ev.data);
        dataChannel.onclose = () => shell.log("Data channel closed", "event");

        shell.log("Requesting microphone access...");
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        for (const t of stream.getAudioTracks()) {
            pc.addTransceiver(t, { direction: "sendrecv" });
        }

        shell.log("Creating WebRTC offer...");
        const offer = await pc.createOffer({ offerToReceiveAudio: 1 });
        await pc.setLocalDescription(offer);

        shell.log("Sending offer...");
        const reply = await send("offer", { sdp: offer.sdp, type: offer.type });
        if (!reply.answer) throw new Error(reply.error || "no answer in reply");
        shell.log(`Got answer (pc_id=${reply.answer.pc_id})`);
        await pc.setRemoteDescription(reply.answer);
    });

    shell.onSendText(async (_text) => {
        // Could extend the proxy to forward typed messages over the data channel.
        shell.log("Text input not wired for WebRTC mode (use voice).", "info");
    });

    shell.onStop(async () => {
        try { ws && ws.send(JSON.stringify({ action: "disconnect", data: {} })); } catch {}
        if (dataChannel) { try { dataChannel.close(); } catch {} dataChannel = null; }
        if (pc) { try { pc.close(); } catch {} pc = null; }
        if (ws) { try { ws.close(); } catch {} ws = null; }
        shell.remoteAudioEl.srcObject = null;
        shell.remoteAudioEl.style.display = "none";
    });
}
