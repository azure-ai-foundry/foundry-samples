// Copyright (c) Microsoft. All rights reserved.

using System.Text.RegularExpressions;

namespace BrowserAutomation;

/// <summary>
/// Redaction utilities for sensitive values in logs and tool output.
/// Equivalent to Python's redact_sensitive_values().
/// </summary>
public static class Redaction
{
    private static readonly Regex TokenPattern = new(@"\beyJ[a-zA-Z0-9._-]{20,}\b", RegexOptions.Compiled);
    private static readonly Regex AccessKeyPattern = new(@"(accessKey=)[^&\s""']+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Redact(string text)
    {
        text = TokenPattern.Replace(text, "<token>");
        text = AccessKeyPattern.Replace(text, "$1<redacted>");
        return text;
    }
}
