// Chat-client UI shell. Loads ./app.js and gives it the `shell` API:
//
//   shell.log(text, cls)
//   shell.setStatus(text)
//   shell.setSessionId(sid)
//   shell.title(text)
//   shell.appendBotText(delta) / finalizeBotText()
//   shell.appendUserPartial(text) / commitUserFinal(text) / resetUserBubble()
//   shell.addToggle({id, label, default, onChange})
//   shell.remoteAudioEl                 — <audio> for the WebRTC remote track
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

// Optional toggle in the controls bar -------------------------------------
function addToggle({ id, label, default: def = false, onChange }) {
    const wrap = document.createElement("label");
    wrap.className = "toggle";
    const input = document.createElement("input");
    input.type = "checkbox";
    input.id = id;
    input.checked = def;
    input.addEventListener("change", () => onChange && onChange(input.checked));
    wrap.appendChild(input);
    wrap.appendChild(document.createTextNode(" " + label));
    const controls = $("controls");
    const status = controls.querySelector(".status");
    controls.insertBefore(wrap, status);
    return input;
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
    addToggle,
    onStart(cb)  { onStartCb  = cb; },
    onStop(cb)   { onStopCb   = cb; },
    onSendText(cb) { onSendTextCb = cb; },
    isConnected: () => connected,
    setTextPlaceholder(t) { $("textInput").placeholder = t; },
    remoteAudioEl: $("remoteAudio"),
};

try {
    const mod = await import("/static/app.js");
    await mod.default(shell);
    log("Client ready", "event");
} catch (e) {
    log(`Failed to load client: ${e.message}`, "error");
}
