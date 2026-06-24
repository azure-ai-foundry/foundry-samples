// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;

namespace BrowserAutomation;

/// <summary>
/// Skills manager — loads embedded markdown skill files for guided workflows.
/// </summary>
public static class Skills
{
    private static readonly Assembly Assembly = typeof(Skills).Assembly;

    /// <summary>Load a skill markdown file by name.</summary>
    public static string? LoadSkill(string name)
    {
        // Sanitize name
        var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        var resourceName = Assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith($".{safeName}.md", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null) return null;

        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>List available skill names.</summary>
    public static List<string> ListSkills()
    {
        return Assembly.GetManifestResourceNames()
            .Where(r => r.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Select(r =>
            {
                // Resource names are like "browser_automation.skills.form-filler.md"
                var parts = r.Split('.');
                return parts.Length >= 2 ? parts[^2] : r;
            })
            .ToList();
    }
}
