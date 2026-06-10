namespace WorkstreamManager.AgentLogic.ResponsesApi.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Utilities for formatting Teams responses, parsing @-mention markup,
/// and extracting identity information from Teams activities.
/// </summary>
internal class TeamsActivityHelper
{
    internal static readonly Regex AtTagRegex = new(
        "<at[^>]*>(?<name>.*?)</at>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ILogger _logger;

    internal TeamsActivityHelper(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Wraps the LLM response in an outbound Activity, prepending a Teams @-mention of the
    /// original sender (the writer of the message we're responding to) when the conversation
    /// is a Teams group chat or channel. In 1:1 personal chats the mention is omitted -
    /// there's no ambiguity about who the agent is talking to.
    /// </summary>
    internal Activity BuildResponseActivity(ITurnContext turnContext, string responseText)
    {
        var channelId = turnContext.Activity.ChannelId?.ToString();
        var conversationType = turnContext.Activity.Conversation?.ConversationType;
        var isGroup = turnContext.Activity.Conversation?.IsGroup;
        var isTeamsGroupOrChannel = string.Equals(channelId, "msteams", StringComparison.OrdinalIgnoreCase)
            && (isGroup == true
                || string.Equals(conversationType, "groupChat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(conversationType, "channel", StringComparison.OrdinalIgnoreCase));

        var sender = turnContext.Activity.From;
        if (!isTeamsGroupOrChannel
            || sender == null
            || string.IsNullOrWhiteSpace(sender.Id)
            || string.IsNullOrWhiteSpace(sender.Name))
        {
            return (Activity)MessageFactory.Text(responseText);
        }

        var encodedName = System.Net.WebUtility.HtmlEncode(sender.Name);
        var mentionText = $"<at>{encodedName}</at>";

        var mention = new Mention
        {
            Mentioned = new ChannelAccount
            {
                Id = sender.Id,
                Name = sender.Name,
            },
            Text = mentionText,
        };

        var quoteBlock = BuildTeamsReplyBlockquote(turnContext.Activity);
        var activityText = quoteBlock != null
            ? $"{quoteBlock}<p>{mentionText} {responseText}</p>"
            : $"{mentionText} {responseText}";

        var activity = (Activity)MessageFactory.Text(activityText);
        activity.Entities ??= new List<Entity>();
        activity.Entities.Add(mention);

        _logger.LogInformation(
            "Outbound response: prepending @-mention of original sender. senderId={SenderId} senderName={SenderName} channelId={ChannelId} conversationType={ConversationType} quotedMessageId={QuotedMessageId}",
            sender.Id,
            sender.Name,
            channelId,
            conversationType,
            quoteBlock != null ? turnContext.Activity.Id : null);

        return activity;
    }

    /// <summary>
    /// Builds the HTML blockquote that Teams interprets as a "quoted reply" (the native UI
    /// that shows the original message above the reply in a quote bubble). Returns null if
    /// the inbound activity is missing fields required for the quote (message id, sender id,
    /// or sender name) - callers should fall back to a plain reply in that case.
    ///
    /// Teams recognizes this rendering when the blockquote carries:
    ///   itemtype="http://schema.skype.com/Reply" and itemid=<originalMessageId>
    /// with child elements identifying the quoted sender's MRI (id), display name, the
    /// original timestamp, and a preview of the quoted text.
    /// </summary>
    internal static string? BuildTeamsReplyBlockquote(IActivity inbound)
    {
        var messageId = inbound.Id;
        var sender = inbound.From;
        if (string.IsNullOrWhiteSpace(messageId) || sender == null
            || string.IsNullOrWhiteSpace(sender.Id) || string.IsNullOrWhiteSpace(sender.Name))
        {
            return null;
        }

        var timestampMs = (inbound.Timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds().ToString();
        var preview = BuildQuotePreviewText(inbound.Text);

        var encodedMessageId = System.Net.WebUtility.HtmlEncode(messageId);
        var encodedSenderMri = System.Net.WebUtility.HtmlEncode(sender.Id);
        var encodedSenderName = System.Net.WebUtility.HtmlEncode(sender.Name);
        var encodedPreview = System.Net.WebUtility.HtmlEncode(preview);

        return
            $"<blockquote itemscope=\"\" itemtype=\"http://schema.skype.com/Reply\" itemid=\"{encodedMessageId}\">" +
                $"<strong itemprop=\"mri\" itemid=\"{encodedSenderMri}\">{encodedSenderName}</strong>" +
                $"<span itemprop=\"time\" itemid=\"{timestampMs}\"></span>" +
                $"<p itemprop=\"preview\">{encodedPreview}</p>" +
            "</blockquote>";
    }

    /// <summary>
    /// Produces a plain-text preview of the quoted message suitable for inclusion in the
    /// blockquote's "preview" element. Converts Teams &lt;at&gt;NAME&lt;/at&gt; mention markup
    /// into "@NAME", strips any other HTML tags, collapses whitespace, and truncates to a
    /// reasonable length so the quote bubble doesn't dwarf the reply.
    /// </summary>
    internal static string BuildQuotePreviewText(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var withAtsConverted = AtTagRegex.Replace(rawText, m =>
        {
            var name = m.Groups["name"]?.Value?.Trim();
            return string.IsNullOrEmpty(name) ? string.Empty : "@" + name;
        });
        var stripped = Regex.Replace(withAtsConverted, @"<[^>]+>", string.Empty);
        var collapsed = Regex.Replace(stripped, @"\s+", " ").Trim();

        const int maxPreviewLength = 200;
        if (collapsed.Length > maxPreviewLength)
        {
            collapsed = collapsed.Substring(0, maxPreviewLength).TrimEnd() + "-";
        }
        return collapsed;
    }

    internal static bool IsTeamsGroupChat(IActivity activity)
    {
        if (!string.Equals(activity.ChannelId?.ToString(), "msteams", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var conversationType = activity.Conversation?.ConversationType;
        if (string.Equals(conversationType, "channel", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(conversationType, "groupChat", StringComparison.OrdinalIgnoreCase)
            || activity.Conversation?.IsGroup == true;
    }

    internal HashSet<string> GetBotCandidateIds(ChannelAccount? recipient)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (recipient == null)
        {
            return candidates;
        }

        var (agenticUserId, agenticAppId, botId, _) = ExtractRecipientAgenticIdentifiers(recipient);
        AddCandidate(candidates, recipient.Id);
        AddCandidate(candidates, recipient.AadObjectId);
        AddCandidate(candidates, recipient.AgenticUserId);
        AddCandidate(candidates, recipient.AgenticAppId);
        AddCandidate(candidates, agenticUserId);
        AddCandidate(candidates, agenticAppId);
        AddCandidate(candidates, botId);

        if (!string.IsNullOrWhiteSpace(recipient.Id))
        {
            var idx = recipient.Id.LastIndexOf(':');
            if (idx >= 0 && idx < recipient.Id.Length - 1)
            {
                AddCandidate(candidates, recipient.Id.Substring(idx + 1));
            }
        }

        return candidates;
    }

    internal static List<string> ExtractConversationMemberIdCandidates(JsonElement member)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (member.TryGetProperty("userId", out var userIdProp) && userIdProp.ValueKind == JsonValueKind.String)
        {
            AddCandidate(candidates, userIdProp.GetString());
        }
        if (member.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
        {
            var id = idProp.GetString();
            AddCandidate(candidates, id);
            if (!string.IsNullOrWhiteSpace(id))
            {
                var idx = id.LastIndexOf(':');
                if (idx >= 0 && idx < id.Length - 1)
                {
                    AddCandidate(candidates, id.Substring(idx + 1));
                }
            }
        }

        return candidates.ToList();
    }

    internal static List<string> GetSenderIdCandidates(ChannelAccount? sender)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (sender == null)
        {
            return candidates.ToList();
        }

        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                candidates.Add(value.Trim());
            }
        }

        Add(sender.AadObjectId);
        Add(sender.Id);
        Add(sender.AgenticUserId);
        if (sender.Properties != null)
        {
            if (sender.Properties.TryGetValue("aadObjectId", out var aadObjProp) && aadObjProp is JsonElement aadObjJson && aadObjJson.ValueKind == JsonValueKind.String)
            {
                Add(aadObjJson.GetString());
            }
            if (sender.Properties.TryGetValue("agenticUserId", out var agenticUserProp) && agenticUserProp is JsonElement agenticUserJson && agenticUserJson.ValueKind == JsonValueKind.String)
            {
                Add(agenticUserJson.GetString());
            }
            if (sender.Properties.TryGetValue("userId", out var userIdProp) && userIdProp is JsonElement userIdJson && userIdJson.ValueKind == JsonValueKind.String)
            {
                Add(userIdJson.GetString());
            }
            if (sender.Properties.TryGetValue("id", out var idProp) && idProp is JsonElement idJson && idJson.ValueKind == JsonValueKind.String)
            {
                Add(idJson.GetString());
            }
        }
        if (!string.IsNullOrWhiteSpace(sender.Id))
        {
            var idx = sender.Id.LastIndexOf(':');
            if (idx >= 0 && idx < sender.Id.Length - 1)
            {
                Add(sender.Id.Substring(idx + 1));
            }
        }

        return candidates.ToList();
    }

    /// <summary>
    /// Pulls candidate names out of Teams-style <at>NAME</at> mention markup in the message text.
    /// Resilient to the SDK stripping the strongly-typed mention entity contents.
    ///
    /// Returns BOTH individual tag names AND the space-joined concatenation of any run of
    /// consecutive <at> tags separated only by whitespace. Teams sometimes splits a multi-word
    /// display name (e.g. "thwagne Groupchat") into separate adjacent tags
    /// (<at>thwagne</at> <at>Groupchat</at>) rather than emitting one tag, so callers need to
    /// consider the joined form as a candidate name. Each name appears at most once and original
    /// ordering is preserved.
    /// </summary>
    internal static List<string> ExtractAtTagNames(string? text)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string name)
        {
            var trimmed = name.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return;
            }
            if (seen.Add(trimmed))
            {
                results.Add(trimmed);
            }
        }

        if (string.IsNullOrEmpty(text))
        {
            return results;
        }

        var matches = AtTagRegex.Matches(text);
        if (matches.Count == 0)
        {
            return results;
        }

        var currentRun = new List<string>();
        int previousEnd = -1;

        foreach (Match m in matches)
        {
            var name = m.Groups["name"]?.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var isContiguous = previousEnd >= 0
                && previousEnd <= m.Index
                && string.IsNullOrWhiteSpace(text.Substring(previousEnd, m.Index - previousEnd));

            if (!isContiguous && currentRun.Count > 1)
            {
                Add(string.Join(' ', currentRun));
            }
            if (!isContiguous)
            {
                currentRun.Clear();
            }

            Add(name);
            currentRun.Add(name);
            previousEnd = m.Index + m.Length;
        }

        if (currentRun.Count > 1)
        {
            Add(string.Join(' ', currentRun));
        }

        return results;
    }

    /// <summary>
    /// Reads the agentic-user / agentic-app identifiers Teams attaches to the recipient channel
    /// account when delivering to an agentic bot. These live on extension properties not always
    /// surfaced by the strongly-typed ChannelAccount, so we JSON-round-trip to extract them.
    /// </summary>
    internal (string? AgenticUserId, string? AgenticAppId, string? BotId, string? Role) ExtractRecipientAgenticIdentifiers(ChannelAccount? recipient)
    {
        if (recipient == null)
        {
            return (null, null, null, null);
        }
        try
        {
            var json = JsonSerializer.Serialize(recipient);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? agenticUserId = null;
            string? agenticAppId = null;
            string? botId = null;
            string? role = null;
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }
                if (string.Equals(prop.Name, "AgenticUserId", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prop.Name, "agenticUserId", StringComparison.OrdinalIgnoreCase))
                {
                    agenticUserId = prop.Value.GetString();
                }
                else if (string.Equals(prop.Name, "AgenticAppId", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(prop.Name, "agenticAppId", StringComparison.OrdinalIgnoreCase))
                {
                    agenticAppId = prop.Value.GetString();
                }
                else if (string.Equals(prop.Name, "botId", StringComparison.OrdinalIgnoreCase))
                {
                    botId = prop.Value.GetString();
                }
                else if (string.Equals(prop.Name, "Role", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(prop.Name, "role", StringComparison.OrdinalIgnoreCase))
                {
                    role = prop.Value.GetString();
                }
            }
            return (agenticUserId, agenticAppId, botId, role);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract agentic identifiers from recipient.");
            return (null, null, null, null);
        }
    }

    internal sealed record MentionInfo(string? MentionedId, string? MentionedName, string? Text);

    /// <summary>
    /// Defensively extracts mention entities from an activity by JSON-round-tripping each entity,
    /// so we don't depend on the precise strongly-typed shape exposed by the SDK version in use.
    /// </summary>
    internal List<MentionInfo> ExtractMentions(IActivity activity)
    {
        var mentions = new List<MentionInfo>();
        var entities = activity.Entities;
        if (entities == null)
        {
            return mentions;
        }

        foreach (var entity in entities)
        {
            if (entity == null)
            {
                continue;
            }

            string? entityType = null;
            try
            {
                entityType = entity.Type;
            }
            catch
            {
            }

            if (!string.Equals(entityType, "mention", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var json = JsonSerializer.Serialize(entity);
                using var doc = JsonDocument.Parse(json);
                string? mentionedId = null;
                string? mentionedName = null;
                string? text = null;

                if (doc.RootElement.TryGetProperty("mentioned", out var mentionedProp) &&
                    mentionedProp.ValueKind == JsonValueKind.Object)
                {
                    if (mentionedProp.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                    {
                        mentionedId = idProp.GetString();
                    }
                    if (mentionedProp.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                    {
                        mentionedName = nameProp.GetString();
                    }
                }
                if (doc.RootElement.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                {
                    text = textProp.GetString();
                }

                mentions.Add(new MentionInfo(mentionedId, mentionedName, text));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse mention entity; skipping.");
            }
        }

        return mentions;
    }

    private static void AddCandidate(HashSet<string> candidates, string? candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            candidates.Add(candidate.Trim());
        }
    }
}

