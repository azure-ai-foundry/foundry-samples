// Copyright (c) Microsoft. All rights reserved.

/*
 * Voice Live Hello World — Bring Your Own Invocations (WebSocket) agent for C#
 *
 * Minimal real-time voice agent. The Invocations SDK
 * (Azure.AI.AgentServer.Invocations) handles the /invocations_ws WebSocket
 * route, OpenTelemetry tracing, and keep-alive pings. The user-supplied
 * VoiceLiveHandler bridges each browser WebSocket connection to a fresh
 * Azure Voice Live session — Voice Live owns the STT, LLM, and TTS
 * pipeline in one managed service, so this sample's only job is to shuttle
 * audio bytes and control events.
 *
 * Wire format with the browser (matches ./chat_client/index.html):
 *
 *   Browser → Server (binary): raw PCM16 mic chunks at 24 kHz mono (Voice
 *     Live's native rate — no resampling).
 *   Browser → Server (text JSON): {"type":"text","content":"..."} —
 *     appended to the Voice Live conversation as a user turn + response.create.
 *   Server → Browser (binary): 8-byte little-endian header
 *     (sample_rate u32, num_channels u32) followed by PCM16 audio.
 *   Server → Browser (text JSON): control events
 *     (session_started, user_speech_started/stopped, transcription,
 *      bot_text, response_done, error).
 *
 * Endpoint resolution:
 *
 *   • In Foundry-hosted runs, FOUNDRY_PROJECT_ENDPOINT is auto-injected
 *     (e.g. https://<acct>.services.ai.azure.com/api/projects/<proj>);
 *     this sample strips the /api/projects/... suffix to get the bare
 *     AI-Services account URL that the Voice Live SDK expects.
 *   • For local runs, set AZURE_VOICELIVE_ENDPOINT (account URL or
 *     project URL — both work).
 *
 * Other environment variables:
 *
 *   AZURE_VOICELIVE_MODEL                       Realtime model (declared in agent.manifest.yaml).
 *   AZURE_VOICELIVE_VOICE                       Default "en-US-Ava:DragonHDLatestNeural".
 *   AZURE_VOICELIVE_INSTRUCTIONS                System prompt.
 *   AZURE_VOICELIVE_IDLE_ENGAGEMENT_SECONDS     Seconds of silence after which
 *                                               the agent proactively re-engages.
 *                                               Set to 0 to disable.
 *
 * Authentication is always DefaultAzureCredential: locally via `az login`;
 * in Foundry via the hosted agent's managed identity.
 *
 * Usage:
 *
 *   az login
 *   export AZURE_VOICELIVE_ENDPOINT=https://<account>.services.ai.azure.com/
 *   dotnet run
 *   # → Kestrel listening on http://0.0.0.0:8088/invocations_ws
 */

using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Azure.AI.AgentServer.Invocations;
using Azure.AI.VoiceLive;
using Azure.Core;
using Azure.Identity;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Load environment variables from a .env file if present (for local development).
Env.NoClobber().TraversePath().Load();

InvocationsServer.Run<VoiceLiveHandler>(configure: builder =>
{
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
        Console.Error.WriteLine(
            "[WARNING] APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent " +
            "to Application Insights. Set it to enable local telemetry. " +
            "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)");

    var endpoint = ResolveVoiceLiveEndpoint();
    var credential = new DefaultAzureCredential();
    var voiceLiveClient = new VoiceLiveClient(endpoint, credential);

    builder.Services.AddSingleton(voiceLiveClient);
    builder.Services.AddSingleton(VoiceLiveConfig.FromEnvironment());
});

static Uri ResolveVoiceLiveEndpoint()
{
    var raw = (Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT") ?? "").Trim();
    if (string.IsNullOrEmpty(raw))
        raw = (Environment.GetEnvironmentVariable("AZURE_VOICELIVE_ENDPOINT") ?? "").Trim();
    if (string.IsNullOrEmpty(raw))
        throw new InvalidOperationException(
            "Neither FOUNDRY_PROJECT_ENDPOINT (auto-injected in hosted containers) nor " +
            "AZURE_VOICELIVE_ENDPOINT (set locally) is present. Set one to your AI Services / " +
            "Foundry endpoint, e.g. 'https://<account>.services.ai.azure.com/'.");

    if (!Uri.TryCreate(raw, UriKind.Absolute, out var parsed)
        || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
    {
        throw new InvalidOperationException(
            $"Invalid Voice Live endpoint '{raw}': expected an absolute URL like " +
            "'https://<account>.services.ai.azure.com/' or a Foundry project URL of the form " +
            "'https://<account>.services.ai.azure.com/api/projects/<proj>'.");
    }

    // Strip any /api/projects/... path; the Voice Live SDK builds its own paths from the account root.
    return new UriBuilder(parsed.Scheme, parsed.Host, parsed.IsDefaultPort ? -1 : parsed.Port) { Path = "/" }.Uri;
}

/// <summary>
/// Per-process Voice Live configuration resolved from environment variables.
/// </summary>
public sealed record VoiceLiveConfig(
    string Model,
    string Voice,
    string Instructions,
    double IdleEngagementSeconds)
{
    public const string DefaultVoice = "en-US-Ava:DragonHDLatestNeural";

    public const string DefaultInstructions =
        "You are a friendly, concise voice assistant. " +
        "Greet the user warmly on the first turn, then keep replies short — " +
        "this is a real-time voice conversation.";

    public static VoiceLiveConfig FromEnvironment()
    {
        var model = (Environment.GetEnvironmentVariable("AZURE_VOICELIVE_MODEL") ?? "gpt-realtime").Trim();
        var voice = (Environment.GetEnvironmentVariable("AZURE_VOICELIVE_VOICE") ?? "").Trim();
        if (string.IsNullOrEmpty(voice)) voice = DefaultVoice;

        var instructions = (Environment.GetEnvironmentVariable("AZURE_VOICELIVE_INSTRUCTIONS") ?? "").Trim();
        if (string.IsNullOrEmpty(instructions)) instructions = DefaultInstructions;

        var raw = Environment.GetEnvironmentVariable("AZURE_VOICELIVE_IDLE_ENGAGEMENT_SECONDS");
        if (!double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var idle))
        {
            idle = 20.0;
        }

        return new VoiceLiveConfig(model, voice, instructions, idle);
    }
}

/// <summary>
/// Per-connection bridge between a browser WebSocket and an Azure Voice Live session.
///
/// The Invocations SDK accepts the WebSocket upgrade before invoking
/// <see cref="HandleWebSocketAsync"/> and cleanly closes the socket on
/// return (close code 1000), so this handler only owns the application
/// payloads — not the handshake or close negotiation.
/// </summary>
public sealed class VoiceLiveHandler(
    VoiceLiveClient voiceLiveClient,
    VoiceLiveConfig config,
    ILogger<VoiceLiveHandler> logger) : InvocationWebSocketHandler
{
    // Voice Live's native realtime audio is PCM16 24 kHz mono. We forward
    // bytes verbatim in both directions, so the browser must capture/play
    // at 24 kHz too.
    private const int SampleRate = 24_000;
    private const int Channels = 1;

    public override async Task HandleWebSocketAsync(
        WebSocket webSocket,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "WebSocket bridge starting (session {SessionId}, invocation {InvocationId})",
            context.SessionId, context.InvocationId);

        await using var session = await voiceLiveClient.StartSessionAsync(
            BuildSessionOptions(config),
            cancellationToken).ConfigureAwait(false);

        var state = new IdleState();
        using var bridgeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var browserToVoiceLive = BrowserToVoiceLiveAsync(webSocket, session, bridgeCts.Token);
        var voiceLiveToBrowser = VoiceLiveToBrowserAsync(webSocket, session, state, bridgeCts.Token);
        var idleWatcher = IdleEngagementWatcherAsync(session, state, config.IdleEngagementSeconds, bridgeCts.Token);

        var finished = await Task.WhenAny(browserToVoiceLive, voiceLiveToBrowser, idleWatcher)
            .ConfigureAwait(false);
        bridgeCts.Cancel();

        // Drain the remaining tasks so any error surfaces in the logs.
        await Task.WhenAll(
            ObserveAsync(browserToVoiceLive, "browser→voicelive"),
            ObserveAsync(voiceLiveToBrowser, "voicelive→browser"),
            ObserveAsync(idleWatcher, "idle-watcher")).ConfigureAwait(false);

        await TryCloseAsync(session, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("WebSocket bridge stopped (session {SessionId})", context.SessionId);

        async Task ObserveAsync(Task t, string name)
        {
            try { await t.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected on shutdown */ }
            catch (Exception ex)
            {
                logger.LogError(ex, "WS bridge task {Name} failed", name);
            }
        }
    }

    // ── Browser → Voice Live ────────────────────────────────────────────────
    private async Task BrowserToVoiceLiveAsync(
        WebSocket webSocket,
        VoiceLiveSession session,
        CancellationToken cancellationToken)
    {
        using var bufferOwner = new MemoryStream();
        var receiveBuffer = new byte[8192];

        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            bufferOwner.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                try
                {
                    result = await webSocket.ReceiveAsync(receiveBuffer, cancellationToken).ConfigureAwait(false);
                }
                catch (WebSocketException ex)
                {
                    logger.LogDebug(ex, "Browser WebSocket receive failed");
                    return;
                }
                if (result.MessageType == WebSocketMessageType.Close) return;
                bufferOwner.Write(receiveBuffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (bufferOwner.Length == 0) continue;
            var payload = bufferOwner.ToArray();

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                // Raw PCM16 mic chunk — forward verbatim into the input audio buffer.
                await session.SendInputAudioAsync(payload, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // Text frame — parse a JSON control message.
            JsonDocument? document;
            try
            {
                document = JsonDocument.Parse(payload);
            }
            catch (JsonException)
            {
                continue;
            }
            using (document)
            {
                if (!document.RootElement.TryGetProperty("type", out var typeProp)) continue;
                var kind = typeProp.GetString();
                if (kind != "text") continue;

                if (!document.RootElement.TryGetProperty("content", out var contentProp)) continue;
                var content = contentProp.GetString();
                if (string.IsNullOrEmpty(content)) continue;

                await session.AddItemAsync(
                    new UserMessageItem(new InputTextContentPart(content)),
                    cancellationToken).ConfigureAwait(false);
                await session.StartResponseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // ── Voice Live → Browser ────────────────────────────────────────────────
    private async Task VoiceLiveToBrowserAsync(
        WebSocket webSocket,
        VoiceLiveSession session,
        IdleState state,
        CancellationToken cancellationToken)
    {
        var greetingSent = false;

        await foreach (var update in session.GetUpdatesAsync(cancellationToken).ConfigureAwait(false))
        {
            switch (update)
            {
                case SessionUpdateSessionUpdated sessionUpdated:
                {
                    await SafeSendJsonAsync(webSocket, new
                    {
                        type = "session_started",
                        session_id = sessionUpdated.Session?.Id,
                    }, cancellationToken).ConfigureAwait(false);

                    // Proactive welcome: once the session is ready, ask the LLM to
                    // greet the user so the bot speaks first.
                    // https://learn.microsoft.com/azure/ai-services/speech-service/how-to-voice-live-proactive-messages
                    if (!greetingSent)
                    {
                        greetingSent = true;
                        try
                        {
                            await session.AddItemAsync(
                                new SystemMessageItem(new InputTextContentPart(
                                    "Greet the user warmly in one short sentence " +
                                    "and invite them to ask a question.")),
                                cancellationToken).ConfigureAwait(false);
                            await session.StartResponseAsync(cancellationToken).ConfigureAwait(false);
                            logger.LogInformation("Sent proactive greeting request");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to send proactive greeting");
                        }
                    }
                    break;
                }

                case SessionUpdateInputAudioBufferSpeechStarted:
                    state.MarkUserActive();
                    await SafeSendJsonAsync(webSocket, new { type = "user_speech_started" }, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case SessionUpdateInputAudioBufferSpeechStopped:
                    state.MarkUserActive();
                    await SafeSendJsonAsync(webSocket, new { type = "user_speech_stopped" }, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case SessionUpdateResponseCreated:
                    state.ResponseInProgress = true;
                    break;

                case SessionUpdateResponseAudioDelta audioDelta:
                {
                    // The SDK already base64-decodes the audio delta, so Delta is
                    // raw PCM16 bytes at the session output rate (24 kHz mono).
                    var pcm = audioDelta.Delta?.ToArray() ?? Array.Empty<byte>();
                    if (pcm.Length == 0) break;

                    // Track real playback duration so idle detection waits until
                    // the bot has actually stopped speaking in the browser.
                    state.AddBotAudio(pcm.Length, SampleRate);
                    await SafeSendBytesAsync(webSocket, PackAudioFrame(pcm, SampleRate, Channels), cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                case SessionUpdateResponseAudioTranscriptDelta transcriptDelta:
                    if (!string.IsNullOrEmpty(transcriptDelta.Delta))
                    {
                        await SafeSendJsonAsync(webSocket, new
                        {
                            type = "bot_text",
                            delta = transcriptDelta.Delta,
                            final = false,
                        }, cancellationToken).ConfigureAwait(false);
                    }
                    break;

                case SessionUpdateResponseAudioTranscriptDone transcriptDone:
                    await SafeSendJsonAsync(webSocket, new
                    {
                        type = "bot_text",
                        delta = "",
                        final = true,
                        text = transcriptDone.Transcript ?? "",
                    }, cancellationToken).ConfigureAwait(false);
                    break;

                case SessionUpdateConversationItemInputAudioTranscriptionCompleted transcribed:
                    await SafeSendJsonAsync(webSocket, new
                    {
                        type = "transcription",
                        text = transcribed.Transcript ?? "",
                        final = true,
                    }, cancellationToken).ConfigureAwait(false);
                    break;

                case SessionUpdateResponseDone:
                    state.ResponseInProgress = false;
                    await SafeSendJsonAsync(webSocket, new { type = "response_done" }, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case SessionUpdateError error:
                    await SafeSendJsonAsync(webSocket, new
                    {
                        type = "error",
                        message = error.Error?.Message ?? "Voice Live error",
                        code = error.Error?.Code,
                    }, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    logger.LogDebug("Voice Live event: {EventType}", update.GetType().Name);
                    break;
            }
        }
    }

    // ── Idle re-engagement ──────────────────────────────────────────────────
    private async Task IdleEngagementWatcherAsync(
        VoiceLiveSession session,
        IdleState state,
        double idleSeconds,
        CancellationToken cancellationToken)
    {
        if (idleSeconds <= 0) return;

        var pollInterval = TimeSpan.FromSeconds(Math.Clamp(idleSeconds / 4.0, 1.0, 5.0));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (state.ResponseInProgress) continue;
            if (state.IdleSeconds() < idleSeconds) continue;

            try
            {
                await session.AddItemAsync(
                    new SystemMessageItem(new InputTextContentPart(
                        "The user has been silent for a while. Re-engage them with one short, " +
                        "friendly sentence — ask if they're still there or offer a topic to explore.")),
                    cancellationToken).ConfigureAwait(false);
                await session.StartResponseAsync(cancellationToken).ConfigureAwait(false);

                logger.LogInformation(
                    "Sent idle re-engagement (idle={Idle:F1}s, threshold={Threshold:F1}s)",
                    state.IdleSeconds(), idleSeconds);

                // Avoid immediately re-firing; the response itself will extend
                // BotAudioEnd as deltas arrive.
                state.Reset();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send idle engagement");
            }
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static VoiceLiveSessionOptions BuildSessionOptions(VoiceLiveConfig config)
    {
        var options = new VoiceLiveSessionOptions
        {
            Model = config.Model,
            Instructions = config.Instructions,
            InputAudioFormat = InputAudioFormat.Pcm16,
            OutputAudioFormat = OutputAudioFormat.Pcm16,
            // Transcribe the user's mic input via Azure Speech so the browser
            // gets `transcription` events alongside the bot's audio.
            InputAudioTranscription = new AudioInputTranscriptionOptions(
                AudioInputTranscriptionOptionsModel.AzureSpeech),
            TurnDetection = new ServerVadTurnDetection
            {
                Threshold = 0.5f,
                PrefixPadding = TimeSpan.FromMilliseconds(300),
                SilenceDuration = TimeSpan.FromMilliseconds(500),
            },
            InputAudioEchoCancellation = new AudioEchoCancellation(),
            InputAudioNoiseReduction = new AudioNoiseReduction(
                AudioNoiseReductionType.AzureDeepNoiseSuppression),
            Voice = BuildVoice(config.Voice),
        };
        options.Modalities.Add(InteractionModality.Text);
        options.Modalities.Add(InteractionModality.Audio);
        return options;
    }

    private static VoiceProvider BuildVoice(string voice)
    {
        // Azure voices look like "en-US-Ava:DragonHDLatestNeural" (contain "-").
        // Bare OpenAI voice names ("alloy", "echo", ...) map to OAIVoice.
        if (voice.Contains('-'))
        {
            return new AzureStandardVoice(voice);
        }
        return new OpenAIVoice(new OAIVoice(voice));
    }

    private static byte[] PackAudioFrame(byte[] pcm, int sampleRate, int channels)
    {
        var frame = new byte[8 + pcm.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(0, 4), (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4, 4), (uint)channels);
        Buffer.BlockCopy(pcm, 0, frame, 8, pcm.Length);
        return frame;
    }

    private static async Task<bool> SafeSendJsonAsync(WebSocket webSocket, object payload, CancellationToken cancellationToken)
    {
        if (webSocket.State != WebSocketState.Open) return false;
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
            await webSocket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SafeSendBytesAsync(WebSocket webSocket, byte[] data, CancellationToken cancellationToken)
    {
        if (webSocket.State != WebSocketState.Open) return false;
        try
        {
            await webSocket.SendAsync(data, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task TryCloseAsync(VoiceLiveSession session, CancellationToken cancellationToken)
    {
        try
        {
            await session.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Swallow — the session may already be closed; the SDK will dispose it.
        }
    }

    /// <summary>
    /// Tracks real silence for idle re-engagement.
    ///
    /// <see cref="_botAudioEndUtc"/> is the wall-clock time at which the
    /// audio streamed to the browser is expected to finish playing,
    /// accumulated from response.audio.delta byte counts at 24 kHz PCM16.
    /// <see cref="_lastUserEventUtc"/> is bumped on any user speech activity.
    /// Effective silence-start is the later of the two.
    /// </summary>
    private sealed class IdleState
    {
        private DateTime _lastUserEventUtc = DateTime.UtcNow;
        private DateTime _botAudioEndUtc = DateTime.UtcNow;

        public bool ResponseInProgress { get; set; }

        public void MarkUserActive() => _lastUserEventUtc = DateTime.UtcNow;

        public void AddBotAudio(int pcmBytes, int sampleRate)
        {
            // PCM16 mono => 2 bytes per sample.
            var duration = TimeSpan.FromSeconds(pcmBytes / 2.0 / sampleRate);
            var now = DateTime.UtcNow;
            var baseline = _botAudioEndUtc > now ? _botAudioEndUtc : now;
            _botAudioEndUtc = baseline + duration;
        }

        public double IdleSeconds()
        {
            var now = DateTime.UtcNow;
            var baseline = _lastUserEventUtc > _botAudioEndUtc ? _lastUserEventUtc : _botAudioEndUtc;
            var idle = (now - baseline).TotalSeconds;
            return idle < 0 ? 0 : idle;
        }

        public void Reset() => _lastUserEventUtc = DateTime.UtcNow;
    }
}
