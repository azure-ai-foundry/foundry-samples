import {
  VoiceLiveClient,
  type ClientEventResponseCreate,
  type VoiceLiveSession,
  type VoiceLiveSubscription,
} from "@azure/ai-voicelive";
import type { AccessToken, TokenCredential } from "@azure/core-auth";

/** Wraps a pre-obtained bearer token as an Azure TokenCredential. */
class StaticTokenCredential implements TokenCredential {
  constructor(private token: string) {}
  async getToken(): Promise<AccessToken> {
    return { token: this.token, expiresOnTimestamp: Date.now() + 3_600_000 };
  }
}

// ---------------------------------------------------------------------------
// DOM refs
// ---------------------------------------------------------------------------
const $messages    = document.getElementById("messages")!;
const $events      = document.getElementById("events")!;
const $badge       = document.getElementById("connectionBadge")!;
const $btnConnect  = document.getElementById("btnConnect") as HTMLButtonElement;
const $btnDisc     = document.getElementById("btnDisconnect") as HTMLButtonElement;
const $hint        = document.getElementById("listeningHint")!;
const $voiceDot    = document.getElementById("voiceStatusDot")!;
const $voiceText   = document.getElementById("voiceStatusText")!;
const $bookingStat = document.getElementById("bookingStatus")!;

const $cfgEndpoint   = document.getElementById("cfgEndpoint")   as HTMLInputElement;
const $cfgAgentName  = document.getElementById("cfgAgentName")  as HTMLInputElement;
const $cfgProjectName = document.getElementById("cfgProjectName") as HTMLInputElement;
const $cfgToken      = document.getElementById("cfgToken")      as HTMLInputElement;
const $cfgVoice      = document.getElementById("cfgVoice")      as HTMLSelectElement;

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------
let session: VoiceLiveSession | null = null;
let subscription: VoiceLiveSubscription | null = null;
let audioContext: AudioContext | null = null;
let currentSources: AudioBufferSourceNode[] = [];
let nextAudioStartTime = 0;

let captureStream: MediaStream | null = null;
let captureWorkletNode: AudioWorkletNode | null = null;

let currentAssistantText = "";
let currentAssistantEl: HTMLElement | null = null;

// ---------------------------------------------------------------------------
// Config persistence
// ---------------------------------------------------------------------------
const STORAGE_KEY = "hotel-voicelive-sdk-config";
const VOICE_LIVE_API_VERSION = "2026-01-01-preview";

function loadConfig() {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      const c = JSON.parse(saved);
      if (c.endpoint)     $cfgEndpoint.value      = c.endpoint;
      if (c.agentName)    $cfgAgentName.value     = c.agentName;
      if (c.projectName)  $cfgProjectName.value   = c.projectName;
      // Avoid persisting access tokens in localStorage.
      if (c.voice)        $cfgVoice.value         = c.voice;
    }
  } catch { /* ignore */ }
}

function saveConfig() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify({
    endpoint:    $cfgEndpoint.value,
    agentName:   $cfgAgentName.value,
    projectName: $cfgProjectName.value,
    token:       $cfgToken.value,
    voice:       $cfgVoice.value,
  }));
}

loadConfig();

// ---------------------------------------------------------------------------
// UI helpers
// ---------------------------------------------------------------------------
function setConnectionState(state: "disconnected" | "connecting" | "connected") {
  $badge.className = `badge-${state}`;
  $badge.textContent = state.charAt(0).toUpperCase() + state.slice(1);
  $btnConnect.disabled = state !== "disconnected";
  $btnDisc.disabled    = state === "disconnected";

  const locked = state !== "disconnected";
  $cfgEndpoint.disabled    = locked;
  $cfgAgentName.disabled   = locked;
  $cfgProjectName.disabled = locked;
  $cfgToken.disabled       = locked;
  $cfgVoice.disabled       = locked;
}

function setVoiceStatus(status: "idle" | "listening" | "processing" | "speaking") {
  $voiceDot.className = status === "idle" ? "" : status;
  const labels: Record<string, string> = {
    idle:       "Idle",
    listening:  "Listening...",
    processing: "Processing...",
    speaking:   "Agent speaking...",
  };
  $voiceText.textContent = labels[status] ?? status;
  $hint.textContent = status === "listening" ? "🎤 Speak now" : "";
}

function setBookingStatus(cls: string, text: string) {
  $bookingStat.className   = cls;
  $bookingStat.textContent = text;
}

function addMessage(role: "user" | "assistant" | "status", text: string): HTMLElement {
  const wrapper = document.createElement("div");
  if (role === "status") {
    wrapper.className = "msg msg-status";
    wrapper.textContent = text;
  } else {
    wrapper.className = `msg-wrapper-${role}`;
    const label = document.createElement("div");
    label.className = "msg-label";
    label.textContent = role === "user" ? "You" : "Hotel Agent";
    const bubble = document.createElement("div");
    bubble.className = `msg msg-${role}`;
    bubble.textContent = text;
    wrapper.append(label, bubble);
  }
  $messages.appendChild(wrapper);
  $messages.scrollTop = $messages.scrollHeight;
  return wrapper;
}

function logEvent(type: string, detail?: string) {
  const entry = document.createElement("div");
  entry.className = "event-entry";
  const ts = new Date().toLocaleTimeString("en-US", { hour12: false });
  entry.innerHTML = `<span class="event-type">${ts}</span> ${escHtml(type)}${detail ? " — " + escHtml(detail.substring(0, 80)) : ""}`;
  $events.appendChild(entry);
  $events.scrollTop = $events.scrollHeight;
}

function escHtml(s: string): string {
  const d = document.createElement("div");
  d.textContent = s;
  return d.innerHTML;
}

// ---------------------------------------------------------------------------
// Hotel UI rendering (invocation delta events)
// ---------------------------------------------------------------------------

interface HotelInfo {
  id: string;
  name: string;
  price: number;
  rating: number;
  features?: string[];
}

interface ActionInfo {
  action: string;
  label: string;
  hotel_id?: string;
}

interface HotelDetailInfo {
  name: string;
  price: number;
  nights: number;
  total: number;
  features?: string[];
}

interface BookingInfo {
  hotel_name: string;
  confirmation_code: string;
  nights: number;
  total: number;
}

function renderHotelCards(hotels: HotelInfo[]) {
  const row = document.createElement("div");
  row.className = "hotel-cards-row";
  hotels.forEach(h => {
    const card = document.createElement("div");
    card.className = "hotel-card";
    const featuresHtml = (h.features ?? [])
      .map(f => `<span>${escHtml(f)}</span>`)
      .join("");
    card.innerHTML = `
      <div class="hc-name">${escHtml(h.name)}</div>
      <div class="hc-price">$${h.price}<span style="font-size:12px;font-weight:400;color:var(--muted)">/night</span></div>
      <div class="hc-rating">★ ${h.rating}</div>
      ${featuresHtml ? `<div class="hc-features">${featuresHtml}</div>` : ""}
    `;
    const btn = document.createElement("button");
    btn.className = "hc-select";
    btn.textContent = "Select";
    btn.addEventListener("click", () => {
      sendInvokeInput({ type: "button_click", action: "select_hotel", hotel_id: h.id });
      btn.disabled = true;
      btn.textContent = "Selected";
    });
    card.appendChild(btn);
    row.appendChild(card);
  });
  $messages.appendChild(row);
  $messages.scrollTop = $messages.scrollHeight;
  setBookingStatus("booking-status-search", "🔍 Showing available hotels");
}

function renderActionButtons(actions: ActionInfo[]) {
  const row = document.createElement("div");
  row.className = "action-buttons-row";
  actions.forEach(a => {
    const btn = document.createElement("button");
    const isConfirm = a.action === "confirm_booking";
    const isCancel  = a.action === "cancel";
    btn.className = `btn-action${isConfirm ? " confirm" : isCancel ? " cancel" : ""}`;
    btn.textContent = a.label;
    btn.addEventListener("click", () => {
      sendInvokeInput({ type: "button_click", action: a.action, hotel_id: a.hotel_id });
      row.querySelectorAll("button").forEach(b => ((b as HTMLButtonElement).disabled = true));
    });
    row.appendChild(btn);
  });
  $messages.appendChild(row);
  $messages.scrollTop = $messages.scrollHeight;
  setBookingStatus("booking-status-select", "🏨 Awaiting your choice");
}

function renderHotelDetail(hotel: HotelDetailInfo) {
  const card = document.createElement("div");
  card.className = "hotel-detail-card";
  const featuresHtml = (hotel.features ?? [])
    .map(f => `<span>${escHtml(f)}</span>`)
    .join("");
  card.innerHTML = `
    <div class="hd-name">${escHtml(hotel.name)}</div>
    <div class="hd-price">$${hotel.price}/night × ${hotel.nights} nights</div>
    <div class="hd-total">$${hotel.total} total</div>
    ${featuresHtml ? `<div class="hd-features">${featuresHtml}</div>` : ""}
  `;
  $messages.appendChild(card);
  $messages.scrollTop = $messages.scrollHeight;
  setBookingStatus("booking-status-select", `📋 ${hotel.name} — $${hotel.total} total`);
}

function renderBookingConfirmed(booking: BookingInfo) {
  const card = document.createElement("div");
  card.className = "booking-confirmed-card";
  card.innerHTML = `
    <div class="bc-icon">✅</div>
    <div class="bc-title">Booking Confirmed!</div>
    <div class="bc-code">${escHtml(booking.confirmation_code)}</div>
    <div class="bc-detail">
      <strong>${escHtml(booking.hotel_name)}</strong><br>
      ${booking.nights} nights — <strong>$${booking.total} total</strong>
    </div>
  `;
  $messages.appendChild(card);
  $messages.scrollTop = $messages.scrollHeight;
  setBookingStatus("booking-status-booked", `✅ Booked: ${booking.hotel_name}`);
}

function renderBookingUpdate(change: string, surcharge: number) {
  const card = document.createElement("div");
  card.className = "booking-update-card";
  card.textContent = `📝 Booking updated: ${change}${surcharge ? ` (+$${surcharge})` : ""}`;
  $messages.appendChild(card);
  $messages.scrollTop = $messages.scrollHeight;
}

// ---------------------------------------------------------------------------
// Invocation delta parser
// ---------------------------------------------------------------------------
function handleInvocationDelta(event: unknown) {
  const evt = event as Record<string, unknown>;
  let delta = evt.delta;
  if (!delta) {
    console.warn("[invocation] empty delta", evt);
    return;
  }

  let parsed: Record<string, unknown>;
  if (typeof delta === "string") {
    let cleaned = delta.trim();
    if (cleaned.startsWith("data: ")) cleaned = cleaned.slice(6).trim();
    if (!cleaned || cleaned === "[DONE]") return;
    try {
      parsed = JSON.parse(cleaned);
    } catch (e) {
      console.warn("[invocation] parse error", delta, e);
      return;
    }
  } else if (typeof delta === "object" && delta !== null) {
    parsed = delta as Record<string, unknown>;
  } else {
    console.warn("[invocation] unknown delta type", typeof delta);
    return;
  }

  const ptype = parsed.type as string | undefined;
  if (!ptype) {
    console.warn("[invocation] no type field", parsed);
    return;
  }

  console.log("[invocation.delta]", ptype, parsed);
  logEvent("invocation.delta", ptype);

  switch (ptype) {
    case "ui.hotel_cards": {
      const hotels = (parsed.hotels ?? []) as HotelInfo[];
      console.log("[hotel_cards] rendering", hotels.length, "hotels");
      renderHotelCards(hotels);
      break;
    }
    case "ui.action_buttons": {
      const actions = (parsed.actions ?? []) as ActionInfo[];
      console.log("[action_buttons] ignored", actions.length, "buttons");
      logEvent("invocation.delta", "ui.action_buttons ignored");
      break;
    }
    case "ui.hotel_detail": {
      const hotel = parsed.hotel as HotelDetailInfo;
      console.log("[hotel_detail]", hotel.name);
      renderHotelDetail(hotel);
      break;
    }
    case "ui.booking_confirmed": {
      const booking = parsed.booking as BookingInfo;
      console.log("[booking_confirmed]", booking.confirmation_code);
      renderBookingConfirmed(booking);
      break;
    }
    case "ui.booking_update": {
      const change = (parsed.change as string) ?? "";
      const surcharge = (parsed.surcharge as number) ?? 0;
      console.log("[booking_update]", change, surcharge);
      renderBookingUpdate(change, surcharge);
      break;
    }
    case "output_audio_transcription.delta": {
      const d = parsed.delta as string ?? "";
      if (d) {
        if (!currentAssistantEl) {
          currentAssistantText = "";
          currentAssistantEl = addMessage("assistant", "");
        }
        currentAssistantText += d;
        const bubble = currentAssistantEl.querySelector(".msg-assistant");
        if (bubble) bubble.textContent = currentAssistantText;
        $messages.scrollTop = $messages.scrollHeight;
      }
      break;
    }
    case "output_audio_transcription.done": {
      const text = parsed.text as string ?? "";
      if (text && currentAssistantEl) {
        const bubble = currentAssistantEl.querySelector(".msg-assistant");
        if (bubble) bubble.textContent = text;
      }
      currentAssistantEl = null;
      currentAssistantText = "";
      break;
    }
    default:
      console.log("[invocation] unhandled type:", ptype, parsed);
  }
}

// ---------------------------------------------------------------------------
// Send button-click events back to the agent
// ---------------------------------------------------------------------------
function sendInvokeInput(payload: Record<string, unknown>) {
  if (!session?.isConnected) return;

  const event: ClientEventResponseCreate = {
    type: "response.create",
    eventId: `event_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
    response: {
      invokeInput: payload,
    },
  };

  session.sendEvent(event).catch((err: Error) => {
    logEvent("sendEvent.error", err.message);
  });

  logEvent("invoke_input", payload.action as string);
}

// ---------------------------------------------------------------------------
// Audio capture — microphone → session (AudioWorklet)
// ---------------------------------------------------------------------------
async function startCapture() {
  if (!session || !audioContext) return;
  captureStream = await navigator.mediaDevices.getUserMedia({
    audio: { sampleRate: 24000, channelCount: 1, echoCancellation: true, noiseSuppression: true },
  });

  const workletCode = `
    class CaptureProcessor extends AudioWorkletProcessor {
      process(inputs) {
        const ch = inputs[0]?.[0];
        if (ch) {
          const i16 = new Int16Array(ch.length);
          for (let i = 0; i < ch.length; i++) {
            const s = Math.max(-1, Math.min(1, ch[i]));
            i16[i] = s < 0 ? s * 0x8000 : s * 0x7FFF;
          }
          this.port.postMessage(i16.buffer, [i16.buffer]);
        }
        return true;
      }
    }
    registerProcessor('hotel-capture-processor', CaptureProcessor);
  `;
  const blob = new Blob([workletCode], { type: "application/javascript" });
  const url  = URL.createObjectURL(blob);
  await audioContext.audioWorklet.addModule(url);
  URL.revokeObjectURL(url);

  const source = audioContext.createMediaStreamSource(captureStream);
  captureWorkletNode = new AudioWorkletNode(audioContext, "hotel-capture-processor");
  captureWorkletNode.port.onmessage = (e: MessageEvent) => {
    if (session?.isConnected) {
      session.sendAudio(new Uint8Array(e.data as ArrayBuffer)).catch(() => {});
    }
  };
  source.connect(captureWorkletNode);
}

function stopCapture() {
  captureWorkletNode?.disconnect();
  captureWorkletNode = null;
  captureStream?.getTracks().forEach(t => t.stop());
  captureStream = null;
}

// ---------------------------------------------------------------------------
// Audio playback — session PCM16 → speakers
// ---------------------------------------------------------------------------
function clearAudioQueue() {
  nextAudioStartTime = 0;
  for (const src of currentSources) {
    try { src.stop(); } catch { /* ignore */ }
  }
  currentSources = [];
}

function queueAudioDelta(delta: unknown) {
  if (!audioContext) return;

  let int16: Int16Array;

  if (delta instanceof ArrayBuffer) {
    int16 = new Int16Array(delta);
  } else if (delta instanceof Uint8Array) {
    const aligned = new ArrayBuffer(delta.byteLength);
    new Uint8Array(aligned).set(delta);
    int16 = new Int16Array(aligned);
  } else if (typeof delta === "string") {
    const raw = atob(delta);
    const bytes = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
    int16 = new Int16Array(bytes.buffer);
  } else {
    return;
  }

  if (int16.length === 0) return;

  const float32 = new Float32Array(int16.length);
  for (let i = 0; i < int16.length; i++) float32[i] = int16[i] / 0x8000;

  const buf = audioContext.createBuffer(1, float32.length, 24000);
  buf.copyToChannel(float32, 0);

  const source = audioContext.createBufferSource();
  source.buffer = buf;
  source.connect(audioContext.destination);

  const now = audioContext.currentTime;
  const startAt = Math.max(now + 0.01, nextAudioStartTime);
  source.start(startAt);
  nextAudioStartTime = startAt + buf.duration;

  currentSources.push(source);
  source.onended = () => {
    const idx = currentSources.indexOf(source);
    if (idx >= 0) currentSources.splice(idx, 1);
  };
}

// ---------------------------------------------------------------------------
// Connect / Disconnect
// ---------------------------------------------------------------------------
async function connect() {
  const endpoint    = $cfgEndpoint.value.trim();
  const agentName   = $cfgAgentName.value.trim();
  const projectName = $cfgProjectName.value.trim();
  const token       = $cfgToken.value.trim();

  if (!endpoint || !agentName || !projectName || !token) {
    alert("Please fill in Endpoint, Agent Name, Project Name, and Access Token.");
    return;
  }

  saveConfig();
  setConnectionState("connecting");
  logEvent("connect", "Initiating...");

  try {
    audioContext = new AudioContext({ sampleRate: 24000 });
    await audioContext.resume();

    const credential = new StaticTokenCredential(token);
    const client = new VoiceLiveClient(endpoint, credential, {
      apiVersion: VOICE_LIVE_API_VERSION,
    });
    logEvent("api.version", VOICE_LIVE_API_VERSION);

    session = client.createSession({
      agent: { agentName, projectName },
    });

    // Build subscription options
    const subscriptionOptions = {
      onSessionUpdated: async (event: unknown) => {
        const e = event as Record<string, unknown>;
        const s = e.session as Record<string, unknown> | undefined;
        logEvent("session.updated", `id=${s?.id ?? "?"}`);
      },

      onConversationItemInputAudioTranscriptionCompleted: async (event: unknown) => {
        const e = event as Record<string, unknown>;
        const transcript = (e.transcript as string) ?? "";
        if (transcript.trim()) {
          addMessage("user", transcript);
          logEvent("user.transcript", transcript.substring(0, 60));
        }
      },

      onResponseAudioTranscriptDelta: async (event: unknown) => {
        const e = event as Record<string, unknown>;
        const delta = (e.delta as string) ?? "";
        if (delta) {
          if (!currentAssistantEl) {
            currentAssistantText = "";
            currentAssistantEl = addMessage("assistant", "");
          }
          currentAssistantText += delta;
          const bubble = currentAssistantEl.querySelector(".msg-assistant");
          if (bubble) bubble.textContent = currentAssistantText;
          $messages.scrollTop = $messages.scrollHeight;
        }
      },

      onResponseAudioTranscriptDone: async (event: unknown) => {
        const e = event as Record<string, unknown>;
        const transcript = (e.transcript as string) ?? "";
        logEvent("response.audio.transcript.done", transcript.substring(0, 60));
        currentAssistantEl   = null;
        currentAssistantText = "";
      },

      onResponseTextDone: async (event: unknown) => {
        const e = event as Record<string, unknown>;
        const text = (e.text as string) ?? "";
        if (text.trim()) {
          addMessage("status", text);
          logEvent("response.text.done", text.substring(0, 60));
        }
      },

      onInputAudioBufferSpeechStarted: async () => {
        setVoiceStatus("listening");
        clearAudioQueue();
        currentAssistantEl   = null;
        currentAssistantText = "";
        logEvent("speech.started");
      },

      onInputAudioBufferSpeechStopped: async () => {
        setVoiceStatus("processing");
        logEvent("speech.stopped");
      },

      onResponseCreated: async () => {
        setVoiceStatus("speaking");
        logEvent("response.created");
      },

      onResponseAudioDelta: async (event: unknown) => {
        const e = event as Record<string, unknown>;
        if (e.delta) {
          queueAudioDelta(e.delta);
          setVoiceStatus("speaking");
        }
      },

      onResponseAudioDone: async () => {
        setVoiceStatus("listening");
        logEvent("response.audio.done");
      },

      onResponseDone: async () => {
        setVoiceStatus("listening");
        currentAssistantEl   = null;
        currentAssistantText = "";
        logEvent("response.done");
      },

      onServerError: async (event: unknown) => {
        const e = event as Record<string, unknown>;
        const err = e.error as Record<string, unknown> | undefined;
        const msg = (err?.message as string) ?? "Unknown error";
        if (msg.includes("no active response")) return;
        logEvent("server.error", msg);
      },

      // Python-equivalent catch-all: inspect raw server event.type directly.
      onServerEvent: async (event: unknown) => {
        console.log("Raw server event:", event);
        const e = event as Record<string, unknown>;
        const eventType = (e.type as string | undefined) ?? "";
        if (eventType === "response.invocation.delta") {
          console.log("event:", e);
          console.log("Invocation delta:", e.delta ?? "");
          logEvent("response.invocation.delta");
          handleInvocationDelta(e);
        }
      },
    };

    subscription = session.subscribe(subscriptionOptions);

    await session.connect();
    logEvent("connected", "WebSocket open");

    // Determine voice type (OpenAI vs Azure Standard)
    const voiceName = $cfgVoice.value;
    const openaiVoices = ["alloy", "echo", "fable", "nova", "shimmer"];
    const voice = openaiVoices.includes(voiceName)
      ? { type: "openai" as const, name: voiceName }
      : { type: "azure-standard" as const, name: voiceName };

    await session.updateSession({
      modalities: ["text", "audio"],
      voice,
      inputAudioFormat: "pcm16",
      outputAudioFormat: "pcm16",
      turnDetection: {
        type: "azure_semantic_vad",
        threshold: 0.5,
        prefixPaddingInMs: 300,
        silenceDurationInMs: 500,
      },
      inputAudioEchoCancellation: { type: "server_echo_cancellation" },
      inputAudioNoiseReduction:   { type: "azure_deep_noise_suppression" },
    });
    logEvent("session.configured");

    // Trigger welcome greeting
    await session.sendEvent({ type: "response.create" });

    await startCapture();

    setConnectionState("connected");
    setVoiceStatus("listening");
    setBookingStatus("booking-status-search", "🏨 Ready — ask me to find a hotel!");
    logEvent("ready", "Mic active");

  } catch (err: unknown) {
    const e = err as Error;
    setConnectionState("disconnected");
    setVoiceStatus("idle");
    logEvent("error", e.message ?? String(err));
    alert(`Connection failed: ${e.message ?? err}`);
  }
}

async function disconnect() {
  logEvent("disconnect", "Cleaning up...");
  stopCapture();
  clearAudioQueue();

  if (subscription) {
    try { await subscription.close(); } catch { /* ignore */ }
    subscription = null;
  }
  if (session) {
    try { await session.disconnect(); } catch { /* ignore */ }
    try { await session.dispose(); } catch { /* ignore */ }
    session = null;
  }
  if (audioContext) {
    try { await audioContext.close(); } catch { /* ignore */ }
    audioContext = null;
  }

  setConnectionState("disconnected");
  setVoiceStatus("idle");
  setBookingStatus("booking-status-idle", "No active booking");
  currentAssistantEl   = null;
  currentAssistantText = "";
  logEvent("disconnected");
}

// ---------------------------------------------------------------------------
// Wire up buttons
// ---------------------------------------------------------------------------
$btnConnect.addEventListener("click", connect);
$btnDisc.addEventListener("click", disconnect);
