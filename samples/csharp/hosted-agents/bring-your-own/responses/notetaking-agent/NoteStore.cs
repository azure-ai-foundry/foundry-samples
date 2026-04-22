// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;

public record NoteEntry(string Note, DateTime Timestamp);

// ──────────────────────────────────────────────────────────────────
// Note storage — JSONL file per session
// ──────────────────────────────────────────────────────────────────

public static class NoteStore
{
    private static readonly object s_lock = new();

    private static string GetFilePath(string sessionId)
    {
        var safeId = string.Join("_", sessionId.Split(Path.GetInvalidFileNameChars()));
        // Write to HOME so files are accessible via the Session Files API.
        var baseDir = Environment.GetEnvironmentVariable("HOME")
            ?? Directory.GetCurrentDirectory();
        return Path.Combine(baseDir, $"notes_{safeId}.jsonl");
    }

    public static NoteEntry SaveNote(string sessionId, string noteText)
    {
        var entry = new NoteEntry(noteText, DateTime.UtcNow);
        var json = JsonSerializer.Serialize(entry);
        lock (s_lock)
        {
            File.AppendAllText(GetFilePath(sessionId), json + Environment.NewLine);
        }
        return entry;
    }

    public static List<NoteEntry> GetNotes(string sessionId)
    {
        var path = GetFilePath(sessionId);
        if (!File.Exists(path)) return new List<NoteEntry>();

        lock (s_lock)
        {
            return File.ReadAllLines(path)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonSerializer.Deserialize<NoteEntry>(line))
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .ToList();
        }
    }
}
