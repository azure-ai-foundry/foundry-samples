namespace WorkstreamManager.AgentLogic.ResponsesApi.Helpers;

using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using WorkstreamManager.AgentLogic.ResponsesApi;
using WorkstreamManager.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

/// <summary>
/// Handles all access control logic: DM restrictions, group chat participant checks,
/// cross-tenant guards, /access commands, and allowlist management.
/// </summary>
internal class AccessControlService
{
    private readonly AgentMetadata _agentMetadata;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly string? _graphAccessToken;
    private readonly HttpClient _httpClient;
    private readonly TeamsActivityHelper _teamsHelper;
    private readonly WorkItemToolHandler _workItemTools;
    private readonly TableClient? _directMessageAllowListTableClient;
    private readonly string _directMessageAllowListWorkerKey;

    private static readonly ConcurrentDictionary<string, ManagerIdentity?> ManagerIdentityCache = new();
    private const string DirectMessageAccessCommandPrefix = "/access";
    private const string ManagerOnboardingCommandPrefix = "/onboarding";
    private const string WorkstreamSummaryCommandPrefix = "/workstreamsummary";
    private const string DirectMessageAllowListRowKey = "allowlist";

    internal AccessControlService(
        AgentMetadata agentMetadata,
        ILogger logger,
        IConfiguration configuration,
        string? graphAccessToken,
        HttpClient httpClient,
        TeamsActivityHelper teamsHelper,
        WorkItemToolHandler workItemTools)
    {
        _agentMetadata = agentMetadata ?? throw new ArgumentNullException(nameof(agentMetadata));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _graphAccessToken = graphAccessToken;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _teamsHelper = teamsHelper ?? throw new ArgumentNullException(nameof(teamsHelper));
        _workItemTools = workItemTools ?? throw new ArgumentNullException(nameof(workItemTools));
        _directMessageAllowListWorkerKey = $"{_agentMetadata.TenantId:D}:{_agentMetadata.UserId:D}";
        _directMessageAllowListTableClient = TryCreateDirectMessageAllowListTableClient();
    }
    /// <summary>
    /// Enforces direct-message access control for Teams personal chats. The digital worker's
    /// manager (resolved from Graph /me/manager) is always allowed and can manage an
    /// additional allowlist using "/access" commands. Members of that persisted allowlist
    /// are also allowed to invoke the LLM.
    /// </summary>
    internal async Task<bool> TryHandleRestrictedDirectMessageAsync(
        ITurnContext turnContext,
        CancellationToken cancellationToken)
    {
        var activity = turnContext.Activity;
        if (!string.Equals(activity.Type, ActivityTypes.Message, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(activity.ChannelId?.ToString(), "msteams", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var conversation = activity.Conversation;
        var isPersonalChat = string.Equals(conversation?.ConversationType, "personal", StringComparison.OrdinalIgnoreCase)
            || conversation?.IsGroup == false;
        if (!isPersonalChat)
        {
            return false;
        }

        var manager = await TryResolveManagerIdentityAsync(cancellationToken);
        var senderCandidates = TeamsActivityHelper.GetSenderIdCandidates(activity.From);
        var allowList = await LoadDirectMessageAllowListAsync(cancellationToken);
        var allowListIds = string.Join(",", allowList.Users.Select(user => user.Id));
        var allowListUpns = string.Join(",", allowList.Users.Select(user => user.UserPrincipalName).Where(v => !string.IsNullOrWhiteSpace(v)));
        var allowListStorageScope = GetDirectMessageAllowListStorageScope();
        _logger.LogInformation(
            "DM access control: evaluating sender. senderCandidates=[{SenderCandidates}] managerId={ManagerId} allowListCount={AllowListCount} allowListIds=[{AllowListIds}] allowListUpns=[{AllowListUpns}] workerTenantId={WorkerTenantId} workerUserId={WorkerUserId} allowListStorage={AllowListStorage}",
            string.Join(",", senderCandidates),
            manager?.Id,
            allowList.Users.Count,
            allowListIds,
            allowListUpns,
            _agentMetadata.TenantId,
            _agentMetadata.UserId,
            allowListStorageScope);
        var isManager = manager != null && senderCandidates.Any(candidate =>
            string.Equals(candidate, manager.Id, StringComparison.OrdinalIgnoreCase));
        if (isManager)
        {
            _logger.LogInformation(
                "DM access control: sender allowed (manager matched). senderCandidates=[{SenderCandidates}] managerId={ManagerId}",
                string.Join(",", senderCandidates),
                manager!.Id);

            if (await TryHandleInitialManagerOnboardingAsync(turnContext, allowList, cancellationToken))
            {
                _logger.LogInformation(
                    "DM access control: initial manager onboarding sent; skipped LLM call. senderCandidates=[{SenderCandidates}] managerId={ManagerId}",
                    string.Join(",", senderCandidates),
                    manager.Id);
                return true;
            }

            if (await TryHandleDirectMessageAccessManagerCommandAsync(turnContext, allowList, cancellationToken))
            {
                _logger.LogInformation(
                    "DM access control: manager command handled; skipped LLM call. senderCandidates=[{SenderCandidates}] managerId={ManagerId}",
                    string.Join(",", senderCandidates),
                    manager.Id);
                return true;
            }

            return false;
        }

        var matchedAllowedUser = allowList.Users.FirstOrDefault(user =>
            senderCandidates.Any(candidate =>
                string.Equals(candidate, user.Id, StringComparison.OrdinalIgnoreCase)));
        if (matchedAllowedUser != null)
        {
            _logger.LogInformation(
                "DM access control: sender allowed (allowlist matched). senderCandidates=[{SenderCandidates}] allowedUserId={AllowedUserId} allowedUserUpn={AllowedUserUpn} allowListCount={AllowListCount} allowListIds=[{AllowListIds}] allowListStorage={AllowListStorage}",
                string.Join(",", senderCandidates),
                matchedAllowedUser.Id,
                matchedAllowedUser.UserPrincipalName,
                allowList.Users.Count,
                allowListIds,
                allowListStorageScope);
            return false;
        }

        var managerLabel = GetManagerLabel(manager);
        var cannedText = GetDirectMessageUnauthorizedResponseText(managerLabel);
        await SendAccessControlResponseAsync(turnContext, cannedText, cancellationToken);

        _logger.LogInformation(
            "DM access control: sender blocked; sent canned response and skipped LLM call. senderCandidates=[{SenderCandidates}] managerId={ManagerId} managerDisplayName={ManagerDisplayName} allowListCount={AllowListCount} allowListIds=[{AllowListIds}] allowListUpns=[{AllowListUpns}] workerTenantId={WorkerTenantId} workerUserId={WorkerUserId} allowListStorage={AllowListStorage}",
            string.Join(",", senderCandidates),
            manager?.Id,
            manager?.DisplayName,
            allowList.Users.Count,
            allowListIds,
            allowListUpns,
            _agentMetadata.TenantId,
            _agentMetadata.UserId,
            allowListStorageScope);
        return true;
    }

    private sealed record GroupChatParticipant(string? DisplayName, List<string> IdCandidates);

    internal async Task<bool> TryHandleRestrictedGroupChatAsync(
        ITurnContext turnContext,
        CancellationToken cancellationToken)
    {
        var activity = turnContext.Activity;
        if (!string.Equals(activity.Type, ActivityTypes.Message, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TeamsActivityHelper.IsTeamsGroupChat(activity))
        {
            return false;
        }

        var manager = await TryResolveManagerIdentityAsync(cancellationToken);
        var allowList = await LoadDirectMessageAllowListAsync(cancellationToken);
        var allowListIds = string.Join(",", allowList.Users.Select(user => user.Id));
        var allowListUpns = string.Join(",", allowList.Users.Select(user => user.UserPrincipalName).Where(v => !string.IsNullOrWhiteSpace(v)));
        var participants = await TryResolveGroupChatParticipantsAsync(turnContext, cancellationToken);

        if (participants == null)
        {
            var managerLabel = GetManagerLabel(manager);
            var cannedText = GetGroupChatUnauthorizedResponseText(managerLabel, 0, []);
            await SendAccessControlResponseAsync(turnContext, cannedText, cancellationToken);
            _logger.LogWarning(
                "Group-chat access control: failed to resolve participants from Graph; blocked response by default. managerId={ManagerId} allowListCount={AllowListCount} allowListIds=[{AllowListIds}] allowListUpns=[{AllowListUpns}] allowListStorage={AllowListStorage} conversationId={ConversationId}",
                manager?.Id,
                allowList.Users.Count,
                allowListIds,
                allowListUpns,
                GetDirectMessageAllowListStorageScope(),
                activity.Conversation?.Id);
            return true;
        }

        var unauthorizedParticipants = participants
            .Where(participant => !IsGroupChatParticipantAllowed(participant, manager, allowList))
            .ToList();
        if (unauthorizedParticipants.Count == 0)
        {
            _logger.LogInformation(
                "Group-chat access control: all participants authorized. participantCount={ParticipantCount} allowListCount={AllowListCount} allowListIds=[{AllowListIds}] conversationId={ConversationId}",
                participants.Count,
                allowList.Users.Count,
                allowListIds,
                activity.Conversation?.Id);
            return false;
        }

        var unauthorizedParticipantLabels = unauthorizedParticipants
            .Select(participant => participant.DisplayName ?? participant.IdCandidates.FirstOrDefault() ?? "(unknown)")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var unauthorizedLabels = string.Join(",", unauthorizedParticipantLabels);
        var unauthorizedIdSets = string.Join(" | ",
            unauthorizedParticipants.Select(participant => string.Join("/", participant.IdCandidates)));

        var managerContact = GetManagerLabel(manager);
        var unauthorizedResponseText = GetGroupChatUnauthorizedResponseText(
            managerContact,
            unauthorizedParticipants.Count,
            unauthorizedParticipantLabels);
        await SendAccessControlResponseAsync(turnContext, unauthorizedResponseText, cancellationToken);

        _logger.LogInformation(
            "Group-chat access control: blocked response due to unauthorized participants. participantCount={ParticipantCount} unauthorizedCount={UnauthorizedCount} unauthorizedLabels=[{UnauthorizedLabels}] unauthorizedIdSets=[{UnauthorizedIdSets}] managerId={ManagerId} allowListCount={AllowListCount} allowListIds=[{AllowListIds}] allowListUpns=[{AllowListUpns}] allowListStorage={AllowListStorage} conversationId={ConversationId}",
            participants.Count,
            unauthorizedParticipants.Count,
            unauthorizedLabels,
            unauthorizedIdSets,
            manager?.Id,
            allowList.Users.Count,
            allowListIds,
            allowListUpns,
            GetDirectMessageAllowListStorageScope(),
            activity.Conversation?.Id);
        return true;
    }

    private bool IsGroupChatParticipantAllowed(
        GroupChatParticipant participant,
        ManagerIdentity? manager,
        DirectMessageAllowListStore allowList)
    {
        if (participant.IdCandidates.Count == 0)
        {
            return false;
        }

        if (manager != null && participant.IdCandidates.Any(candidate =>
            string.Equals(candidate, manager.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return allowList.Users.Any(user =>
            participant.IdCandidates.Any(candidate =>
                string.Equals(candidate, user.Id, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<List<GroupChatParticipant>?> TryResolveGroupChatParticipantsAsync(
        ITurnContext turnContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_graphAccessToken))
        {
            _logger.LogWarning("Group-chat access control: Graph token unavailable; cannot resolve chat participants.");
            return null;
        }

        var conversationId = turnContext.Activity.Conversation?.Id;
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            _logger.LogWarning("Group-chat access control: conversationId is missing; cannot resolve chat participants.");
            return null;
        }

        var botCandidateIds = _teamsHelper.GetBotCandidateIds(turnContext.Activity.Recipient);
        var url = $"https://graph.microsoft.com/v1.0/chats/{Uri.EscapeDataString(conversationId)}/members";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _graphAccessToken);
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Group-chat access control: Graph chat-members lookup failed. conversationId={ConversationId} status={Status} body={Body}",
                    conversationId,
                    (int)resp.StatusCode,
                    body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("value", out var members) || members.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning(
                    "Group-chat access control: Graph response missing 'value' array. conversationId={ConversationId} body={Body}",
                    conversationId,
                    body);
                return null;
            }

            var participants = new List<GroupChatParticipant>();
            var memberCount = 0;
            foreach (var member in members.EnumerateArray())
            {
                memberCount++;
                var displayName = member.TryGetProperty("displayName", out var dnProp) && dnProp.ValueKind == JsonValueKind.String
                    ? dnProp.GetString()
                    : null;
                var idCandidates = TeamsActivityHelper.ExtractConversationMemberIdCandidates(member);
                if (idCandidates.Any(candidate => botCandidateIds.Contains(candidate)))
                {
                    continue;
                }

                participants.Add(new GroupChatParticipant(displayName, idCandidates));
            }

            _logger.LogInformation(
                "Group-chat access control: resolved chat members. conversationId={ConversationId} totalMemberCount={TotalMemberCount} participantCount={ParticipantCount} botCandidateIds=[{BotCandidateIds}]",
                conversationId,
                memberCount,
                participants.Count,
                string.Join(",", botCandidateIds));
            return participants;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Group-chat access control: exception while resolving chat participants. conversationId={ConversationId}",
                conversationId);
            return null;
        }
    }

    private static void AddCandidate(HashSet<string> candidates, string? candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            candidates.Add(candidate.Trim());
        }
    }

    private string GetManagerLabel(ManagerIdentity? manager)
    {
        return manager?.DisplayName
            ?? manager?.UserPrincipalName
            ?? _configuration["DirectMessageManagerContact"]
            ?? "my manager";
    }

    private string GetGroupChatUnauthorizedResponseText(
        string managerLabel,
        int unauthorizedCount,
        IReadOnlyList<string> unauthorizedParticipantLabels)
    {
        var template = _configuration["GroupChatUnauthorizedResponse"];
        if (string.IsNullOrWhiteSpace(template))
        {
            template = "I can only respond in this group chat when every participant has been approved by {Manager}. Missing approvals for: {UnauthorizedParticipants}.";
        }

        var participantList = unauthorizedParticipantLabels.Count == 0
            ? "(unknown)"
            : string.Join(", ", unauthorizedParticipantLabels);

        return template
            .Replace("{Manager}", managerLabel, StringComparison.OrdinalIgnoreCase)
            .Replace("{UnauthorizedCount}", unauthorizedCount.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{UnauthorizedParticipants}", participantList, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ManagerIdentity(string Id, string? DisplayName, string? UserPrincipalName);

    private enum DirectMessageAccessCommandKind
    {
        None,
        List,
        Add,
        Remove,
        Help
    }

    private sealed class DirectMessageAllowListStore
    {
        public int Version { get; set; } = 1;

        public List<DirectMessageAllowListUser> Users { get; set; } = [];

        public DateTimeOffset? ManagerOnboardingSentAtUtc { get; set; }
    }

    private sealed class DirectMessageAllowListUser
    {
        public string Id { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string? UserPrincipalName { get; set; }

        public DateTimeOffset AddedAtUtc { get; set; }
    }

    private sealed record DirectMessageAccessCommand(DirectMessageAccessCommandKind Kind, string? Argument);

    private async Task<ManagerIdentity?> TryResolveManagerIdentityAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_graphAccessToken))
        {
            _logger.LogWarning("DM access control: Graph token unavailable; cannot resolve manager identity.");
            return null;
        }

        var cacheKey = $"{_agentMetadata.TenantId:D}:{_agentMetadata.UserId:D}";
        if (ManagerIdentityCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var url = "https://graph.microsoft.com/v1.0/me/manager?$select=id,displayName,userPrincipalName";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _graphAccessToken);
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "DM access control: manager lookup failed. status={Status} body={Body}",
                    (int)resp.StatusCode,
                    body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var id = root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                ? idProp.GetString()
                : null;
            var displayName = root.TryGetProperty("displayName", out var dnProp) && dnProp.ValueKind == JsonValueKind.String
                ? dnProp.GetString()
                : null;
            var userPrincipalName = root.TryGetProperty("userPrincipalName", out var upnProp) && upnProp.ValueKind == JsonValueKind.String
                ? upnProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogWarning("DM access control: manager lookup returned no id.");
                return null;
            }

            var manager = new ManagerIdentity(id, displayName, userPrincipalName);
            ManagerIdentityCache[cacheKey] = manager;
            _logger.LogInformation(
                "DM access control: resolved manager identity. managerId={ManagerId} managerDisplayName={ManagerDisplayName}",
                manager.Id,
                manager.DisplayName);
            return manager;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DM access control: exception while resolving manager identity.");
            return null;
        }
    }

    private string GetDirectMessageUnauthorizedResponseText(string managerLabel)
    {
        var template = _configuration["DirectMessageUnauthorizedResponse"];
        if (string.IsNullOrWhiteSpace(template))
        {
            template = "I can only respond to direct messages from {Manager} or users that they have approved. Please reach out to them for assistance.";
        }

        return template.Replace("{Manager}", managerLabel, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> TryHandleDirectMessageAccessManagerCommandAsync(
        ITurnContext turnContext,
        DirectMessageAllowListStore allowList,
        CancellationToken cancellationToken)
    {
        if (IsManagerOnboardingCommand(turnContext.Activity.Text))
        {
            await SendAccessControlResponseAsync(
                turnContext,
                BuildManagerOnboardingMessage(),
                cancellationToken);
            return true;
        }

        if (await _workItemTools.TryHandleSummaryCommandAsync(turnContext, WorkstreamSummaryCommandPrefix, SendAccessControlResponseAsync, cancellationToken))
        {
            return true;
        }

        var command = ParseDirectMessageAccessCommand(turnContext.Activity.Text);
        if (command.Kind == DirectMessageAccessCommandKind.None)
        {
            return false;
        }

        if (command.Kind == DirectMessageAccessCommandKind.Help)
        {
            await SendAccessControlResponseAsync(
                turnContext,
                BuildDirectMessageAccessCommandHelpText(),
                cancellationToken);
            return true;
        }

        if (command.Kind == DirectMessageAccessCommandKind.List)
        {
            if (allowList.Users.Count == 0)
            {
                await SendAccessControlResponseAsync(
                    turnContext,
                    "The direct-message allowlist is empty.",
                    cancellationToken);
                return true;
            }

            var lines = allowList.Users
                .OrderBy(user => user.DisplayName ?? user.UserPrincipalName ?? user.Id, StringComparer.OrdinalIgnoreCase)
                .Select((user, index) =>
                {
                    var label = user.DisplayName ?? user.UserPrincipalName ?? user.Id;
                    return $"{index + 1}. {label} ({user.Id})";
                });

            var response = "Allowed direct-message users:\n" + string.Join('\n', lines);
            _logger.LogInformation(
                "DM access control: manager requested allowlist list. allowListCount={AllowListCount} allowListIds=[{AllowListIds}] allowListUpns=[{AllowListUpns}] allowListStorage={AllowListStorage} workerTenantId={WorkerTenantId} workerUserId={WorkerUserId}",
                allowList.Users.Count,
                string.Join(",", allowList.Users.Select(user => user.Id)),
                string.Join(",", allowList.Users.Select(user => user.UserPrincipalName).Where(v => !string.IsNullOrWhiteSpace(v))),
                GetDirectMessageAllowListStorageScope(),
                _agentMetadata.TenantId,
                _agentMetadata.UserId);
            await SendAccessControlResponseAsync(turnContext, response, cancellationToken);
            return true;
        }

        if (string.IsNullOrWhiteSpace(command.Argument))
        {
            await SendAccessControlResponseAsync(
                turnContext,
                BuildDirectMessageAccessCommandHelpText(),
                cancellationToken);
            return true;
        }

        var targetIdentity = await TryResolveDirectoryUserIdentityForAccessCommandAsync(
            turnContext,
            command.Argument,
            cancellationToken);
        if (targetIdentity == null)
        {
            await SendAccessControlResponseAsync(
                turnContext,
                $"I couldn't resolve '{command.Argument}' to an Entra user. Use a user object id (GUID), UPN/email, or a Teams contact-card mention.",
                cancellationToken);
            return true;
        }

        if (command.Kind == DirectMessageAccessCommandKind.Add)
        {
            var existing = allowList.Users.FirstOrDefault(user =>
                string.Equals(user.Id, targetIdentity.Id, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                await SendAccessControlResponseAsync(
                    turnContext,
                    $"'{targetIdentity.DisplayName ?? targetIdentity.UserPrincipalName ?? targetIdentity.Id}' is already in the allowlist.",
                    cancellationToken);
                return true;
            }

            allowList.Users.Add(new DirectMessageAllowListUser
            {
                Id = targetIdentity.Id,
                DisplayName = targetIdentity.DisplayName,
                UserPrincipalName = targetIdentity.UserPrincipalName,
                AddedAtUtc = DateTimeOffset.UtcNow,
            });
            await SaveDirectMessageAllowListAsync(allowList, cancellationToken);
            _logger.LogInformation(
                "DM access control: allowlist add applied. addedUserId={AddedUserId} addedUserUpn={AddedUserUpn} allowListCount={AllowListCount} allowListIds=[{AllowListIds}] allowListStorage={AllowListStorage}",
                targetIdentity.Id,
                targetIdentity.UserPrincipalName,
                allowList.Users.Count,
                string.Join(",", allowList.Users.Select(user => user.Id)),
                GetDirectMessageAllowListStorageScope());

            await SendAccessControlResponseAsync(
                turnContext,
                $"Added '{targetIdentity.DisplayName ?? targetIdentity.UserPrincipalName ?? targetIdentity.Id}' to the direct-message allowlist.",
                cancellationToken);
            return true;
        }

        var removed = allowList.Users.RemoveAll(user =>
            string.Equals(user.Id, targetIdentity.Id, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
        {
            await SaveDirectMessageAllowListAsync(allowList, cancellationToken);
            _logger.LogInformation(
                "DM access control: allowlist remove applied. removedUserId={RemovedUserId} removedUserUpn={RemovedUserUpn} allowListCount={AllowListCount} allowListIds=[{AllowListIds}] allowListStorage={AllowListStorage}",
                targetIdentity.Id,
                targetIdentity.UserPrincipalName,
                allowList.Users.Count,
                string.Join(",", allowList.Users.Select(user => user.Id)),
                GetDirectMessageAllowListStorageScope());
            await SendAccessControlResponseAsync(
                turnContext,
                $"Removed '{targetIdentity.DisplayName ?? targetIdentity.UserPrincipalName ?? targetIdentity.Id}' from the direct-message allowlist.",
                cancellationToken);
            return true;
        }

        await SendAccessControlResponseAsync(
            turnContext,
            $"'{targetIdentity.DisplayName ?? targetIdentity.UserPrincipalName ?? targetIdentity.Id}' is not in the direct-message allowlist.",
            cancellationToken);
        return true;
    }

    private async Task<bool> TryHandleInitialManagerOnboardingAsync(
        ITurnContext turnContext,
        DirectMessageAllowListStore allowList,
        CancellationToken cancellationToken)
    {
        if (allowList.ManagerOnboardingSentAtUtc.HasValue)
        {
            return false;
        }

        allowList.ManagerOnboardingSentAtUtc = DateTimeOffset.UtcNow;
        await SaveDirectMessageAllowListAsync(allowList, cancellationToken);
        await SendAccessControlResponseAsync(
            turnContext,
            BuildManagerOnboardingMessage(),
            cancellationToken);

        _logger.LogInformation(
            "DM access control: sent manager onboarding message on first manager DM. onboardingSentAtUtc={OnboardingSentAtUtc} allowListStorage={AllowListStorage} workerTenantId={WorkerTenantId} workerUserId={WorkerUserId}",
            allowList.ManagerOnboardingSentAtUtc,
            GetDirectMessageAllowListStorageScope(),
            _agentMetadata.TenantId,
            _agentMetadata.UserId);
        return true;
    }

    private DirectMessageAccessCommand ParseDirectMessageAccessCommand(string? messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return new DirectMessageAccessCommand(DirectMessageAccessCommandKind.None, null);
        }

        var trimmed = messageText.Trim();
        if (!trimmed.StartsWith(DirectMessageAccessCommandPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new DirectMessageAccessCommand(DirectMessageAccessCommandKind.None, null);
        }

        var parts = trimmed.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return new DirectMessageAccessCommand(DirectMessageAccessCommandKind.Help, null);
        }

        var verb = parts[1];
        var argument = parts.Length >= 3 ? parts[2] : null;
        if (string.Equals(verb, "list", StringComparison.OrdinalIgnoreCase))
        {
            return new DirectMessageAccessCommand(DirectMessageAccessCommandKind.List, null);
        }
        if (string.Equals(verb, "add", StringComparison.OrdinalIgnoreCase))
        {
            return new DirectMessageAccessCommand(DirectMessageAccessCommandKind.Add, argument);
        }
        if (string.Equals(verb, "remove", StringComparison.OrdinalIgnoreCase))
        {
            return new DirectMessageAccessCommand(DirectMessageAccessCommandKind.Remove, argument);
        }
        if (string.Equals(verb, "help", StringComparison.OrdinalIgnoreCase))
        {
            return new DirectMessageAccessCommand(DirectMessageAccessCommandKind.Help, null);
        }

        return new DirectMessageAccessCommand(DirectMessageAccessCommandKind.Help, null);
    }

    private string BuildDirectMessageAccessCommandHelpText()
    {
        return
            "Direct-message access commands:\n" +
            $"{DirectMessageAccessCommandPrefix} list\n" +
            $"{DirectMessageAccessCommandPrefix} add <user-object-id-or-upn-or-mention>\n" +
            $"{DirectMessageAccessCommandPrefix} remove <user-object-id-or-upn-or-mention>";
    }

    private bool IsManagerOnboardingCommand(string? messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return false;
        }

        var trimmed = messageText.Trim();
        return string.Equals(trimmed, ManagerOnboardingCommandPrefix, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "/onboard", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildManagerOnboardingMessage()
    {
        return
            "Hi! I'm your new digital worker. Before I start helping broadly, please configure who I can work with.\n\n" +
            "Documentation: <link-tbd>\n\n" +
            "Run this anytime to see setup guidance again:\n" +
            $"{ManagerOnboardingCommandPrefix}\n\n" +
            BuildDirectMessageAccessCommandHelpText() + "\n\n" +
            "I track work items automatically. When you mention action items, tasks, or deliverables, I'll log them as work items with a 📌 reaction.\n\n" +
            $"Use {WorkstreamSummaryCommandPrefix} run to see all open items grouped by owner.\n\n" +
            "Quick help:\n" +
            "/access help\n\n" +
            "Suggested setup:\n" +
            "1. Define access with /access add <user-object-id-or-upn> for each approved user.\n" +
            "2. Verify with /access list.";
    }

    internal async Task SendAccessControlResponseAsync(
        ITurnContext turnContext,
        string responseText,
        CancellationToken cancellationToken)
    {
        var enableStreamingUpdates = _configuration.GetValue<bool>("EnableStreamingUpdates");
        var inChannelId = turnContext.Activity.ChannelId?.ToString();
        var inConversationType = turnContext.Activity.Conversation?.ConversationType;
        var inIsGroup = turnContext.Activity.Conversation?.IsGroup;
        var isTeamsGroupOrChannel = string.Equals(inChannelId, "msteams", StringComparison.OrdinalIgnoreCase)
            && (inIsGroup == true
                || string.Equals(inConversationType, "groupChat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(inConversationType, "channel", StringComparison.OrdinalIgnoreCase));
        var isMessage = string.Equals(turnContext.Activity.Type, ActivityTypes.Message, StringComparison.OrdinalIgnoreCase);
        if (enableStreamingUpdates && isMessage && !isTeamsGroupOrChannel)
        {
            turnContext.StreamingResponse.QueueTextChunk(responseText);
            return;
        }

        await turnContext.SendActivityAsync((Activity)MessageFactory.Text(responseText), cancellationToken);
    }

    internal async Task<bool> TryHandleCrossTenantActivityAsync(
        ITurnContext turnContext,
        CancellationToken cancellationToken)
    {
        var activity = turnContext.Activity;
        var sender = activity.From;
        var senderTenantCandidates = GetSenderTenantIdCandidates(sender);
        if (senderTenantCandidates.Count == 0)
        {
            return false;
        }

        var agentTenantId = _agentMetadata.TenantId;
        if (agentTenantId == Guid.Empty)
        {
            _logger.LogWarning(
                "AP tenant guard: agent tenant id is empty; skipping cross-tenant enforcement. senderTenantCandidates=[{SenderTenantCandidates}]",
                string.Join(",", senderTenantCandidates));
            return false;
        }

        var isSameTenant = senderTenantCandidates.Any(candidate => TenantIdsMatch(candidate, agentTenantId));
        if (isSameTenant)
        {
            return false;
        }

        var responseText = GetCrossTenantUnauthorizedResponseText();
        await SendAccessControlResponseAsync(turnContext, responseText, cancellationToken);
        _logger.LogInformation(
            "AP tenant guard: sender blocked as out-of-tenant; sent canned response and skipped processing. senderId={SenderId} senderName={SenderName} senderTenantCandidates=[{SenderTenantCandidates}] agentTenantId={AgentTenantId}",
            sender?.Id,
            sender?.Name,
            string.Join(",", senderTenantCandidates),
            agentTenantId);
        return true;
    }

    private static List<string> GetSenderTenantIdCandidates(ChannelAccount? sender)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (sender == null)
        {
            return candidates.ToList();
        }

        static void Add(HashSet<string> set, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                set.Add(value.Trim());
            }
        }

        Add(candidates, sender.TenantId);
        if (sender.Properties != null)
        {
            if (sender.Properties.TryGetValue("tenantId", out var tenantIdProp))
            {
                var tenantIdJson = tenantIdProp;
                if (tenantIdJson.ValueKind == JsonValueKind.String)
                {
                    Add(candidates, tenantIdJson.GetString());
                }
                else if (tenantIdJson.ValueKind == JsonValueKind.Object
                    && tenantIdJson.TryGetProperty("id", out var nestedId)
                    && nestedId.ValueKind == JsonValueKind.String)
                {
                    Add(candidates, nestedId.GetString());
                }
            }

            if (sender.Properties.TryGetValue("tenant", out var tenantProp))
            {
                var tenantJson = tenantProp;
                if (tenantJson.ValueKind == JsonValueKind.String)
                {
                    Add(candidates, tenantJson.GetString());
                }
                else if (tenantJson.ValueKind == JsonValueKind.Object
                    && tenantJson.TryGetProperty("id", out var nestedId)
                    && nestedId.ValueKind == JsonValueKind.String)
                {
                    Add(candidates, nestedId.GetString());
                }
            }
        }

        return candidates.ToList();
    }

    private static bool TenantIdsMatch(string candidate, Guid agentTenantId)
    {
        if (Guid.TryParse(candidate, out var parsed))
        {
            return parsed == agentTenantId;
        }

        return string.Equals(candidate, agentTenantId.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }

    private string GetCrossTenantUnauthorizedResponseText()
    {
        var template = _configuration["CrossTenantUnauthorizedResponse"];
        if (string.IsNullOrWhiteSpace(template))
        {
            return "I can only process activity protocol messages from users in my home tenant.";
        }

        return template.Replace("{TenantId}", _agentMetadata.TenantId.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DirectoryUserIdentity(string Id, string? DisplayName, string? UserPrincipalName);

    private async Task<DirectoryUserIdentity?> TryResolveDirectoryUserIdentityForAccessCommandAsync(
        ITurnContext turnContext,
        string rawIdentifier,
        CancellationToken cancellationToken)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var trimmed = value.Trim();
            if (seen.Add(trimmed))
            {
                candidates.Add(trimmed);
            }
        }

        var commandAtNames = TeamsActivityHelper.ExtractAtTagNames(rawIdentifier);
        if (commandAtNames.Count == 0)
        {
            Add(rawIdentifier);
        }
        foreach (var atName in commandAtNames)
        {
            Add(atName);
        }

        foreach (var mention in _teamsHelper.ExtractMentions(turnContext.Activity))
        {
            Add(mention.MentionedId);
            Add(mention.MentionedName);
            var mentionAtNames = TeamsActivityHelper.ExtractAtTagNames(mention.Text);
            if (mentionAtNames.Count == 0)
            {
                Add(mention.Text);
            }
            foreach (var atName in mentionAtNames)
            {
                Add(atName);
            }
        }

        foreach (var atName in TeamsActivityHelper.ExtractAtTagNames(turnContext.Activity.Text))
        {
            Add(atName);
        }

        foreach (var candidate in candidates)
        {
            var resolved = await TryResolveDirectoryUserIdentityAsync(candidate, cancellationToken);
            if (resolved != null)
            {
                return resolved;
            }
        }

        _logger.LogWarning(
            "DM access control: failed to resolve access target from all candidates. rawIdentifier={RawIdentifier} candidates=[{Candidates}]",
            rawIdentifier,
            string.Join(",", candidates));
        return null;
    }

    private async Task<DirectoryUserIdentity?> TryResolveDirectoryUserIdentityAsync(
        string rawIdentifier,
        CancellationToken cancellationToken)
    {
        var identifier = rawIdentifier.Trim();
        var atTagNames = TeamsActivityHelper.ExtractAtTagNames(identifier);
        if (atTagNames.Count > 0)
        {
            identifier = atTagNames[0];
        }
        else if (identifier.StartsWith("@", StringComparison.Ordinal))
        {
            identifier = identifier.Substring(1).Trim();
        }

        var mriDelimiterIndex = identifier.LastIndexOf(':');
        if (mriDelimiterIndex >= 0 && mriDelimiterIndex < identifier.Length - 1)
        {
            var mriSuffix = identifier.Substring(mriDelimiterIndex + 1);
            if (Guid.TryParse(mriSuffix, out var parsedMriGuid))
            {
                identifier = parsedMriGuid.ToString("D");
            }
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        // If we already have a GUID, we can use it directly even if Graph lookup fails.
        static DirectoryUserIdentity BuildGuidIdentity(string candidate)
            => new(Guid.Parse(candidate).ToString("D"), null, null);

        if (string.IsNullOrWhiteSpace(_graphAccessToken))
        {
            if (Guid.TryParse(identifier, out _))
            {
                return BuildGuidIdentity(identifier);
            }

            _logger.LogWarning(
                "DM access control: cannot resolve non-GUID allowlist identifier without Graph token. identifier={Identifier}",
                identifier);
            return null;
        }

        var url = $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(identifier)}?$select=id,displayName,userPrincipalName";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _graphAccessToken);
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "DM access control: user lookup failed. identifier={Identifier} status={Status} body={Body}",
                    identifier,
                    (int)resp.StatusCode,
                    body);
            }
            else
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var id = root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                    ? idProp.GetString()
                    : null;
                var displayName = root.TryGetProperty("displayName", out var dnProp) && dnProp.ValueKind == JsonValueKind.String
                    ? dnProp.GetString()
                    : null;
                var userPrincipalName = root.TryGetProperty("userPrincipalName", out var upnProp) && upnProp.ValueKind == JsonValueKind.String
                    ? upnProp.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(id))
                {
                    return Guid.TryParse(identifier, out _) ? BuildGuidIdentity(identifier) : null;
                }

                return new DirectoryUserIdentity(id, displayName, userPrincipalName);
            }

            if (Guid.TryParse(identifier, out _))
            {
                return BuildGuidIdentity(identifier);
            }

            var escapedIdentifier = identifier.Replace("'", "''", StringComparison.Ordinal);
            var searchUrl =
                "https://graph.microsoft.com/v1.0/users" +
                "?$select=id,displayName,userPrincipalName,mail" +
                "&$top=25" +
                $"&$filter=displayName eq '{escapedIdentifier}' or userPrincipalName eq '{escapedIdentifier}' or mail eq '{escapedIdentifier}'";
            using var searchReq = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            searchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _graphAccessToken);
            using var searchResp = await _httpClient.SendAsync(searchReq, cancellationToken);
            var searchBody = await searchResp.Content.ReadAsStringAsync(cancellationToken);
            if (!searchResp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "DM access control: user-search fallback failed. identifier={Identifier} status={Status} body={Body}",
                    identifier,
                    (int)searchResp.StatusCode,
                    searchBody);
                return null;
            }

            using var searchDoc = JsonDocument.Parse(searchBody);
            if (!searchDoc.RootElement.TryGetProperty("value", out var usersProp) ||
                usersProp.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning(
                    "DM access control: user-search fallback returned unexpected payload. identifier={Identifier} body={Body}",
                    identifier,
                    searchBody);
                return null;
            }

            var matches = new List<DirectoryUserIdentity>();
            foreach (var user in usersProp.EnumerateArray())
            {
                var id = user.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                    ? idProp.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var displayName = user.TryGetProperty("displayName", out var dnProp) && dnProp.ValueKind == JsonValueKind.String
                    ? dnProp.GetString()
                    : null;
                var userPrincipalName = user.TryGetProperty("userPrincipalName", out var upnProp) && upnProp.ValueKind == JsonValueKind.String
                    ? upnProp.GetString()
                    : null;
                var mail = user.TryGetProperty("mail", out var mailProp) && mailProp.ValueKind == JsonValueKind.String
                    ? mailProp.GetString()
                    : null;

                var isStrongMatch =
                    string.Equals(displayName, identifier, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(userPrincipalName, identifier, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mail, identifier, StringComparison.OrdinalIgnoreCase);

                if (isStrongMatch)
                {
                    matches.Add(new DirectoryUserIdentity(id, displayName, userPrincipalName));
                }
            }

            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count > 1)
            {
                _logger.LogWarning(
                    "DM access control: user-search fallback is ambiguous. identifier={Identifier} matchCount={MatchCount} matchedUsers=[{MatchedUsers}]",
                    identifier,
                    matches.Count,
                    string.Join(",", matches.Select(m => $"{m.DisplayName ?? "(null)"}<{m.UserPrincipalName ?? m.Id}>")));
                return null;
            }

            // No strict match found; if exactly one row came back from Graph, accept it.
            if (usersProp.GetArrayLength() == 1)
            {
                var only = usersProp.EnumerateArray().First();
                var onlyId = only.TryGetProperty("id", out var onlyIdProp) && onlyIdProp.ValueKind == JsonValueKind.String
                    ? onlyIdProp.GetString()
                    : null;
                if (!string.IsNullOrWhiteSpace(onlyId))
                {
                    var onlyDisplayName = only.TryGetProperty("displayName", out var onlyDnProp) && onlyDnProp.ValueKind == JsonValueKind.String
                        ? onlyDnProp.GetString()
                        : null;
                    var onlyUpn = only.TryGetProperty("userPrincipalName", out var onlyUpnProp) && onlyUpnProp.ValueKind == JsonValueKind.String
                        ? onlyUpnProp.GetString()
                        : null;
                    return new DirectoryUserIdentity(onlyId, onlyDisplayName, onlyUpn);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "DM access control: exception while resolving user identity. identifier={Identifier}",
                identifier);
            return Guid.TryParse(identifier, out _) ? BuildGuidIdentity(identifier) : null;
        }
    }

    private TableClient? TryCreateDirectMessageAllowListTableClient()
    {
        try
        {
            var tableName = _configuration["DirectMessageAllowListTableName"];
            if (string.IsNullOrWhiteSpace(tableName))
            {
                tableName = "digitalworkerallowlist";
            }

            var connectionString = _configuration["DirectMessageAllowListTableConnectionString"];
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return new TableServiceClient(connectionString).GetTableClient(tableName);
            }

            var serviceUriValue = _configuration["DirectMessageAllowListTableServiceUri"];
            if (string.IsNullOrWhiteSpace(serviceUriValue))
            {
                _logger.LogWarning(
                    "DM access control: Azure Table Storage is not configured. Set DirectMessageAllowListTableServiceUri or DirectMessageAllowListTableConnectionString.");
                return null;
            }

            if (!Uri.TryCreate(serviceUriValue, UriKind.Absolute, out var serviceUri))
            {
                _logger.LogWarning(
                    "DM access control: DirectMessageAllowListTableServiceUri is invalid: {ServiceUri}",
                    serviceUriValue);
                return null;
            }

            var managedIdentityClientId = _configuration["DirectMessageAllowListManagedIdentityClientId"];
            if (string.IsNullOrWhiteSpace(managedIdentityClientId))
            {
                managedIdentityClientId = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_DEFAULT_INSTANCE_CLIENT_ID");
            }

            var credential = string.IsNullOrWhiteSpace(managedIdentityClientId)
                ? new DefaultAzureCredential()
                : new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = managedIdentityClientId,
                });

            return new TableServiceClient(serviceUri, credential).GetTableClient(tableName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DM access control: failed to initialize Azure Table client for allowlist.");
            return null;
        }
    }

    private string GetDirectMessageAllowListStorageScope()
    {
        if (_directMessageAllowListTableClient == null)
        {
            return "(not-configured)";
        }

        return $"{_directMessageAllowListTableClient.Uri} partitionKey={_directMessageAllowListWorkerKey} rowKey={DirectMessageAllowListRowKey}";
    }

    private static void NormalizeDirectMessageAllowListStore(DirectMessageAllowListStore store)
    {
        store.Users ??= [];
        store.Users = store.Users
            .Where(user => !string.IsNullOrWhiteSpace(user.Id))
            .GroupBy(user => user.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                first.Id = group.Key;
                return first;
            })
            .ToList();
    }

    private async Task<DirectMessageAllowListStore> LoadDirectMessageAllowListAsync(CancellationToken cancellationToken)
    {
        var client = _directMessageAllowListTableClient;
        if (client == null)
        {
            return new DirectMessageAllowListStore();
        }

        try
        {
            var response = await client.GetEntityIfExistsAsync<TableEntity>(
                _directMessageAllowListWorkerKey,
                DirectMessageAllowListRowKey,
                cancellationToken: cancellationToken);
            if (!response.HasValue)
            {
                _logger.LogInformation(
                    "DM access control: allowlist row not found; using empty list. allowListStorage={AllowListStorage} workerTenantId={WorkerTenantId} workerUserId={WorkerUserId}",
                    GetDirectMessageAllowListStorageScope(),
                    _agentMetadata.TenantId,
                    _agentMetadata.UserId);
                return new DirectMessageAllowListStore();
            }

            var entity = response.Value;
            if (entity == null)
            {
                _logger.LogInformation(
                    "DM access control: allowlist row was null; using empty list. allowListStorage={AllowListStorage} workerTenantId={WorkerTenantId} workerUserId={WorkerUserId}",
                    GetDirectMessageAllowListStorageScope(),
                    _agentMetadata.TenantId,
                    _agentMetadata.UserId);
                return new DirectMessageAllowListStore();
            }
            var usersJson = entity.TryGetValue("UsersJson", out var usersJsonObj) ? usersJsonObj?.ToString() : null;
            var versionValue = entity.TryGetValue("Version", out var versionObj) && int.TryParse(versionObj?.ToString(), out var parsedVersion)
                ? parsedVersion
                : 1;
            var managerOnboardingSentAtUtc = entity.TryGetValue("ManagerOnboardingSentAtUtc", out var managerOnboardingSentAtUtcObj)
                ? ResponsesApiClient.TryParseDateTimeOffsetProperty(managerOnboardingSentAtUtcObj)
                : null;

            var store = new DirectMessageAllowListStore
            {
                Version = versionValue,
                Users = string.IsNullOrWhiteSpace(usersJson)
                    ? []
                    : (JsonSerializer.Deserialize<List<DirectMessageAllowListUser>>(usersJson) ?? []),
                ManagerOnboardingSentAtUtc = managerOnboardingSentAtUtc,
            };
            NormalizeDirectMessageAllowListStore(store);

            _logger.LogInformation(
                "DM access control: loaded allowlist from Azure Table. allowListCount={AllowListCount} allowListIds=[{AllowListIds}] allowListUpns=[{AllowListUpns}] allowListStorage={AllowListStorage} workerTenantId={WorkerTenantId} workerUserId={WorkerUserId}",
                store.Users.Count,
                string.Join(",", store.Users.Select(user => user.Id)),
                string.Join(",", store.Users.Select(user => user.UserPrincipalName).Where(v => !string.IsNullOrWhiteSpace(v))),
                GetDirectMessageAllowListStorageScope(),
                _agentMetadata.TenantId,
                _agentMetadata.UserId);
            return store;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation(
                "DM access control: allowlist table or row not found; using empty list. allowListStorage={AllowListStorage} workerTenantId={WorkerTenantId} workerUserId={WorkerUserId}",
                GetDirectMessageAllowListStorageScope(),
                _agentMetadata.TenantId,
                _agentMetadata.UserId);
            return new DirectMessageAllowListStore();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DM access control: failed to load allowlist from Azure Table; using empty list.");
            return new DirectMessageAllowListStore();
        }
    }

    private async Task SaveDirectMessageAllowListAsync(DirectMessageAllowListStore store, CancellationToken cancellationToken)
    {
        var client = _directMessageAllowListTableClient;
        if (client == null)
        {
            _logger.LogWarning("DM access control: cannot save allowlist because Azure Table Storage is not configured.");
            return;
        }

        try
        {
            NormalizeDirectMessageAllowListStore(store);
            await client.CreateIfNotExistsAsync(cancellationToken);

            var entity = new TableEntity(_directMessageAllowListWorkerKey, DirectMessageAllowListRowKey)
            {
                ["Version"] = store.Version,
                ["UsersJson"] = JsonSerializer.Serialize(store.Users),
                ["UpdatedAtUtc"] = DateTimeOffset.UtcNow,
            };
            if (store.ManagerOnboardingSentAtUtc.HasValue)
            {
                entity["ManagerOnboardingSentAtUtc"] = store.ManagerOnboardingSentAtUtc.Value;
            }
            await client.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);

            _logger.LogInformation(
                "DM access control: saved allowlist to Azure Table. allowListCount={AllowListCount} allowListIds=[{AllowListIds}] allowListUpns=[{AllowListUpns}] allowListStorage={AllowListStorage} workerTenantId={WorkerTenantId} workerUserId={WorkerUserId}",
                store.Users.Count,
                string.Join(",", store.Users.Select(user => user.Id)),
                string.Join(",", store.Users.Select(user => user.UserPrincipalName).Where(v => !string.IsNullOrWhiteSpace(v))),
                GetDirectMessageAllowListStorageScope(),
                _agentMetadata.TenantId,
                _agentMetadata.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DM access control: failed to save allowlist to Azure Table.");
        }
    }

}

