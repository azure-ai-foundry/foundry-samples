// Chat-client UI shell. Loads ./app.js and gives it the `shell` API:
//
//   shell.log(text, cls)
//   shell.setStatus(text)
//   shell.setSessionId(sid)
//   shell.title(text)
//   shell.appendBotText(delta) / finalizeBotText()
//   shell.appendUserPartial(text) / commitUserFinal(text) / resetUserBubble()
//   shell.startMic({sampleRate, chunkSamples, onChunk}) / stopMic()
//   shell.playPcm(int16Bytes, {sampleRate, channels}) / stopPlayback()
//   shell.setTextPlaceholder(t)
//   shell.onStart(cb) / onStop(cb) / onSendText(cb)
//
// app.js exports `default async function init(shell)`.

const $ = (id) => document.getElementById(id);

function tsNow() {
    const d = new Date();
    const pad = (n, w = 2) => String(n).padStart(w, "0");
    return `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}.${pad(d.getMilliseconds(), 3)}`;
}
function escapeHtml(s) {
    const d = document.createElement("span"); d.textContent = s; return d.innerHTML;
}

const logPanel = $("logPanel");

function log(text, cls = "info") {
    const div = document.createElement("div");
    div.className = `log-entry ${cls}`;
    div.innerHTML = `<span class="ts">${tsNow()}</span>${escapeHtml(text)}`;
    logPanel.appendChild(div);
    requestAnimationFrame(() => { logPanel.scrollTop = logPanel.scrollHeight; });
}
function setStatus(t) { $("statusText").textContent = t; }
function setSessionId(sid) {
    const el = $("sessionId");
    if (sid) { el.textContent = sid; el.classList.remove("empty"); }
    else { el.textContent = "—"; el.classList.add("empty"); }
}
function setTitle(t) { $("title").textContent = t; }

// Streaming text bubbles ---------------------------------------------------
let pendingBotLine = null;
let pendingUserLine = null;
let lastUserFinal = null;

function appendBotText(delta) {
    if (!delta) return;
    if (!pendingBotLine) {
        pendingBotLine = document.createElement("div");
        pendingBotLine.className = "log-entry bot-text";
        pendingBotLine.innerHTML = `<span class="ts">${tsNow()}</span>Bot: `;
        logPanel.appendChild(pendingBotLine);
    }
    pendingBotLine.appendChild(document.createTextNode(delta));
    logPanel.scrollTop = logPanel.scrollHeight;
}
function finalizeBotText() { pendingBotLine = null; }

function appendUserPartial(text) {
    if (!pendingUserLine) {
        pendingUserLine = document.createElement("div");
        pendingUserLine.className = "log-entry user-text";
        pendingUserLine.innerHTML = `<span class="ts">${tsNow()}</span>User: `;
        logPanel.appendChild(pendingUserLine);
    }
    const ts = pendingUserLine.querySelector("span.ts");
    pendingUserLine.innerHTML = "";
    pendingUserLine.appendChild(ts);
    pendingUserLine.appendChild(document.createTextNode("User: " + (text || "")));
    logPanel.scrollTop = logPanel.scrollHeight;
}
function commitUserFinal(text) {
    if (text && text === lastUserFinal) return;
    lastUserFinal = text;
    if (pendingUserLine) {
        const ts = pendingUserLine.querySelector("span.ts");
        pendingUserLine.innerHTML = "";
        pendingUserLine.appendChild(ts);
        pendingUserLine.appendChild(document.createTextNode("User: " + (text || "")));
        pendingUserLine = null;
    } else {
        log("User: " + text, "user-text");
    }
}
function resetUserBubble() {
    pendingUserLine = null;
    lastUserFinal = null;
}

// Mic capture --------------------------------------------------------------
let micStream = null, micCtx = null, micWorklet = null;

async function startMic({ sampleRate, chunkSamples, onChunk }) {
    micStream = await navigator.mediaDevices.getUserMedia({
        audio: {
            sampleRate,
            channelCount: 1,
            echoCancellation: true,
            noiseSuppression: true,
            autoGainControl: true,
        },
    });
    micCtx = new AudioContext({ sampleRate });
    await micCtx.audioWorklet.addModule("/static/audio-processor.js");
    const src = micCtx.createMediaStreamSource(micStream);
    micWorklet = new AudioWorkletNode(micCtx, "mic-processor", {
        processorOptions: { chunkSamples: chunkSamples || Math.round(sampleRate / 10) },
    });
    micWorklet.port.onmessage = (e) => onChunk(e.data); // ArrayBuffer of Int16
    const silent = micCtx.createGain();
    silent.gain.value = 0;
    src.connect(micWorklet);
    micWorklet.connect(silent);
    silent.connect(micCtx.destination);
    log(`Microphone started (${sampleRate} Hz mono)`, "event");
}

function stopMic() {
    if (micWorklet) { try { micWorklet.disconnect(); } catch {} micWorklet = null; }
    if (micCtx)   { try { micCtx.close(); } catch {} micCtx = null; }
    if (micStream) { micStream.getTracks().forEach((t) => t.stop()); micStream = null; }
}

// Audio playback queue (PCM16 LE) -----------------------------------------
let playCtx = null, nextPlayTime = 0;

function playPcm(int16Bytes, { sampleRate = 24000, channels = 1 } = {}) {
    if (!playCtx || playCtx.sampleRate !== sampleRate) {
        if (playCtx) playCtx.close();
        playCtx = new AudioContext({ sampleRate });
        nextPlayTime = 0;
    }
    const view = int16Bytes instanceof Uint8Array
        ? new Int16Array(int16Bytes.buffer, int16Bytes.byteOffset, int16Bytes.byteLength / 2)
        : int16Bytes;
    if (view.length === 0) return;
    const numSamples = Math.floor(view.length / channels);
    const buf = playCtx.createBuffer(channels, numSamples, sampleRate);
    for (let ch = 0; ch < channels; ch++) {
        const chData = buf.getChannelData(ch);
        for (let i = 0; i < numSamples; i++) {
            chData[i] = view[i * channels + ch] / 32768.0;
        }
    }
    const src = playCtx.createBufferSource();
    src.buffer = buf;
    src.connect(playCtx.destination);
    const now = playCtx.currentTime;
    if (nextPlayTime < now) nextPlayTime = now;
    src.start(nextPlayTime);
    nextPlayTime += buf.duration;
}

function stopPlayback() {
    if (playCtx) { try { playCtx.close(); } catch {} playCtx = null; nextPlayTime = 0; }
}

// Lifecycle ---------------------------------------------------------------
let onStartCb = async () => {};
let onStopCb  = async () => {};
let onSendTextCb = async () => {};
let connected = false;

async function start() {
    $("btnStart").disabled = true;
    $("btnStop").disabled = false;
    setStatus("Connecting");
    resetUserBubble();
    try {
        await onStartCb();
        $("textInput").disabled = false;
        $("btnSend").disabled = false;
        connected = true;
    } catch (e) {
        log("connect error: " + e.message, "error");
        setStatus("Error");
        await stop();
    }
}
async function stop() {
    try { await onStopCb(); } catch (e) { log("stop error: " + e.message, "error"); }
    stopMic();
    stopPlayback();
    pendingBotLine = null;
    pendingUserLine = null;
    connected = false;
    $("btnStart").disabled = false;
    $("btnStop").disabled = true;
    $("textInput").disabled = true;
    $("btnSend").disabled = true;
    setStatus("Idle");
    setSessionId("");
}
function sendText() {
    const text = $("textInput").value.trim();
    if (!text || !connected) return;
    $("textInput").value = "";
    log("User: " + text, "user-text");
    onSendTextCb(text).catch((e) => log("send error: " + e.message, "error"));
}

$("btnStart").onclick = () => start();
$("btnStop").onclick  = () => stop();
$("btnSend").onclick  = () => sendText();
$("textInput").addEventListener("keydown", (e) => { if (e.key === "Enter") sendText(); });

const shell = {
    log, setStatus, setSessionId, title: setTitle,
    appendBotText, finalizeBotText,
    appendUserPartial, commitUserFinal, resetUserBubble,
    startMic, stopMic,
    playPcm, stopPlayback,
    onStart(cb)  { onStartCb  = cb; },
    onStop(cb)   { onStopCb   = cb; },
    onSendText(cb) { onSendTextCb = cb; },
    isConnected: () => connected,
    setTextPlaceholder(t) { $("textInput").placeholder = t; },
};

try {
    const mod = await import("/static/app.js");
    await mod.default(shell);
    log("Client ready", "event");
} catch (e) {
    log(`Failed to load client: ${e.message}`, "error");
}
