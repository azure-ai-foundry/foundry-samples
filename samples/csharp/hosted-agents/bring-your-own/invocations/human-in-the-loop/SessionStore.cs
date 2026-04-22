// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Manages human-in-the-loop session state with JSON file persistence.
/// State is stored in $HOME so files are accessible via the Session Files API.
/// </summary>
public static class SessionStore
{
    private static readonly string StateDir =
        Environment.GetEnvironmentVariable("HOME") ?? Directory.GetCurrentDirectory();

    private static readonly ConcurrentDictionary<string, HitlSession> Sessions = new();
    private static readonly ConcurrentDictionary<string, string> InvocationToSession = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SessionLocks = new();

    /// <summary>Load all persisted sessions into memory on startup.</summary>
    public static void LoadAllSessions()
    {
        if (!Directory.Exists(StateDir))
            return;

        foreach (var path in Directory.EnumerateFiles(StateDir, "hitl_session_*.json"))
        {
            try
            {
                var json = File.ReadAllText(path);
                var session = JsonSerializer.Deserialize<HitlSession>(json);
                if (session is null || string.IsNullOrEmpty(session.SessionId))
                    continue;

                var requiredFields = !string.IsNullOrEmpty(session.Status)
                    && !string.IsNullOrEmpty(session.OriginalTask);
                if (!requiredFields)
                    continue;

                Sessions[session.SessionId] = session;

                foreach (var invId in session.InvocationIds)
                    InvocationToSession[invId] = session.SessionId;
            }
            catch
            {
                // Skip corrupt files
            }
        }

        if (!Sessions.IsEmpty)
            Console.WriteLine($"Loaded {Sessions.Count} session(s) from disk");
    }

    public static HitlSession? GetBySession(string sessionId) =>
        Sessions.TryGetValue(sessionId, out var s) ? s : null;

    public static HitlSession? GetByInvocation(string invocationId)
    {
        var sessionId = GetSessionIdByInvocation(invocationId);
        return sessionId is not null ? GetBySession(sessionId) : null;
    }

    public static string? GetSessionIdByInvocation(string invocationId) =>
        InvocationToSession.TryGetValue(invocationId, out var sid) ? sid : null;

    /// <summary>Persist session state atomically to a JSON file in $HOME.</summary>
    public static void Save(string sessionId, HitlSession session)
    {
        Sessions[sessionId] = session;
        var target = GetFilePath(sessionId);
        var tempPath = target + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, target, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }
    }

    public static void TrackInvocation(string invocationId, string sessionId) =>
        InvocationToSession[invocationId] = sessionId;

    public static SemaphoreSlim GetLock(string sessionId) =>
        SessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));

    private static string GetFilePath(string sessionId)
    {
        var safeId = new string(sessionId.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sessionId));
        var hashSuffix = Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
        return Path.Combine(StateDir, $"hitl_session_{safeId}_{hashSuffix}.json");
    }
}

// ──────────────────────────────────────────────────────────────────
// Session models
// ──────────────────────────────────────────────────────────────────

public class HitlSession
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("original_task")]
    public string OriginalTask { get; set; } = "";

    [JsonPropertyName("proposal")]
    public string Proposal { get; set; } = "";

    [JsonPropertyName("revision_history")]
    public List<RevisionEntry> RevisionHistory { get; set; } = new();

    [JsonPropertyName("invocation_id")]
    public string InvocationId { get; set; } = "";

    [JsonPropertyName("invocation_ids")]
    public List<string> InvocationIds { get; set; } = new();
}

public class RevisionEntry
{
    [JsonPropertyName("proposal")]
    public string Proposal { get; set; } = "";

    [JsonPropertyName("feedback")]
    public string Feedback { get; set; } = "";
}
