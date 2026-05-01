// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.Connector;
using Microsoft.Agents.Extensions.Teams.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TeamsAgent;

public class TeamsAgent : AgentApplication
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TeamsAgent> _logger;
    public TeamsAgent(AgentApplicationOptions options, IHttpClientFactory httpClientFactory, ILogger<TeamsAgent> logger) : base(options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        RegisterExtension(new TeamsAgentExtension(this), tae =>
        {
            tae.OnMessageEdit(MessageEdited);
            tae.MessageExtensions.OnQuery("findNuGetPackage", OnQuery);
            tae.MessageExtensions.OnSelectItem(OnSelectItem);
            tae.MessageExtensions.OnQueryLink(OnQueryLink);
#pragma warning disable CS0618 // Type or member is obsolete
            tae.OnFeedbackLoop(MyFeedbackLoopHandler);
#pragma warning restore CS0618 // Type or member is obsolete
        });
        AdaptiveCards.OnSearch("dataset", OnSearchDS);
        OnMessageReactionsAdded(OnMessageReaction);
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync);
    }

    private Task MyFeedbackLoopHandler(ITurnContext turnContext, ITurnState turnState, FeedbackLoopData feedbackLoopData, CancellationToken cancellationToken)
    {
        // Do something with FeedbackLoopData
        System.Diagnostics.Trace.WriteLine("FeedbackLoop handler");
        return Task.CompletedTask;
    }

    private Task<MessagingExtensionResult> OnQueryLink(ITurnContext turnContext, ITurnState turnState, string url, CancellationToken cancellationToken)
    {
        return Task.FromResult(new MessagingExtensionResult() { Text = "On Query Link" });
    }

    private Task<IList<AdaptiveCardsSearchResult>> OnSearchDS(ITurnContext turnContext, ITurnState turnState, Query<AdaptiveCardsSearchParams> query, CancellationToken cancellationToken)
    {
        var qt = query.Parameters.QueryText;
        IList<AdaptiveCardsSearchResult> result = new List<AdaptiveCardsSearchResult>()
        {
            new AdaptiveCardsSearchResult("search", qt)
        };
        return Task.FromResult(result);
    }

    private async Task<MessagingExtensionResult> OnSelectItem(ITurnContext turnContext, ITurnState turnState, object item, CancellationToken cancellationToken)
    {
        PackageItem? package = JsonSerializer.Deserialize<PackageItem>((JsonElement)item);
        if (package is null)
        {
            await turnContext.SendActivityAsync("selected item is not a packageItem", cancellationToken: cancellationToken);
            _logger.LogWarning("Selected Item cannot be deserialized as a PackageItem");
            return null!;
        }

        await turnContext.SendActivityAsync("selected item " + JsonSerializer.Serialize(item), cancellationToken: cancellationToken);
        ThumbnailCard card = new()
        {
            Title = $"{package.PackageId}, {package.Version}",
            Subtitle = package.Description,
            Buttons =
                [
                    new() { Type = ActionTypes.OpenUrl, Title = "Nuget Package", Value = $"https://www.nuget.org/packages/{package.PackageId}" },
                    new() { Type = ActionTypes.OpenUrl, Title = "Project", Value = package.ProjectUrl},
                ],
        };

        if (!string.IsNullOrEmpty(package.IconUrl))
        {
            card.Images = [new(package.IconUrl, "Icon")];
        }

        MessagingExtensionAttachment attachment = new()
        {
            ContentType = ThumbnailCard.ContentType,
            Content = card,
        };

        return await Task.FromResult(new MessagingExtensionResult
        {
            Type = "result",
            AttachmentLayout = "list",
            Attachments = [attachment]
        });
    }

    private async Task<MessagingExtensionResult> OnQuery(ITurnContext turnContext, ITurnState turnState, Query<IDictionary<string, object>> query, CancellationToken cancellationToken)
    {
        CommandValue<string> cmd = ProtocolJsonSerializer.ToObject<CommandValue<string>>(turnContext.Activity.Value);
        if (cmd.CommandId != "findNuGetPackage")
        {
            _logger.LogWarning("Received unexpected commandID {cmdName}", cmd.CommandId);
            return await Task.FromResult(new MessagingExtensionResult());
        }
        JsonElement el = default;
        if (query.Parameters.TryGetValue("NuGetPackageName", out var elObj))
        {
            if (elObj is JsonElement element)
            {
                el = element;
            }
            else
            {
                _logger.LogWarning("Received unexpected type for NuGetPackageName: {type}", elObj.GetType());
                return await Task.FromResult(new MessagingExtensionResult());
            }
        }
        else
        {
            _logger.LogWarning("Query Parameters does not include NuGetPackageName");
            return await Task.FromResult(new MessagingExtensionResult());
        }

        if (el.ValueKind == JsonValueKind.Undefined)
        {
            return await Task.FromResult(new MessagingExtensionResult());
        }

        string text = el.GetString() ?? string.Empty;


        IEnumerable<PackageItem> packages = await FindPackages(text);
        List<MessagingExtensionAttachment> attachments = [.. packages.Select(package =>
        {
            string cardValue = $$$"""
            {
                "id": "{{{package.PackageId}}}",
                "version" : "{{{package.Version}}}",
                "description" : "{{{PackageItem.NormalizeString(package.Description!)}}}",
                "projectUrl" : "{{{package.ProjectUrl}}}",
                "iconUrl" : "{{{package.IconUrl}}}"
            }
            """;

            ThumbnailCard previewCard = new() { Title = package.PackageId, Tap = new CardAction { Type = "invoke", Value = cardValue } };
            if (!string.IsNullOrEmpty(package.IconUrl))
            {
                previewCard.Images = [new CardImage(package.IconUrl, "Icon")];
            }

            MessagingExtensionAttachment attachment = new()
            {
                ContentType = HeroCard.ContentType,
                Content = new HeroCard { Title = package.Id },
                Preview = previewCard.ToAttachment()
            };

            return attachment;
        })];

        return new MessagingExtensionResult
        {
            Type = "result",
            AttachmentLayout = "list",
            Attachments = attachments

        };
    }

    private async Task<IEnumerable<PackageItem>> FindPackages(string text)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient();
        string jsonResult = await httpClient.GetStringAsync($"https://azuresearch-usnc.nuget.org/query?q=id:{text}&prerelease=true");
        JsonElement data = JsonDocument.Parse(jsonResult).RootElement.GetProperty("data");
        PackageItem[]? packages = data.Deserialize<PackageItem[]>();
        return packages!;
    }

    private Task OnMessageReaction(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
        turnContext.SendActivityAsync("Message Reaction: " + turnContext.Activity.ReactionsAdded[0].Type, cancellationToken: cancellationToken);

    private Task MessageEdited(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
        turnContext.SendActivityAsync("Message Edited: " + turnContext.Activity.Id, cancellationToken: cancellationToken);

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        TeamsChannelAccount member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
        string msg = member.Name ?? "not teams user";
        await turnContext.SendActivityAsync($"hi {msg}, use the '+' option on Teams message textbox to start the MessageExtension search", cancellationToken: cancellationToken);
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var numFiles = turnState.Temp.InputFiles.Count;
        await turnContext.SendActivityAsync("found files " + numFiles);
    }
}

class Root
{
    [JsonPropertyName("data")]
    public PackageItem[]? Data { get; set; }
}

class PackageItem
{
    [JsonPropertyName("@id")]
    public string? Id { get; set; }

    [JsonPropertyName("id")]
    public string? PackageId { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("projectUrl")]
    public string? ProjectUrl { get; set; }

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }
    public static string NormalizeString(string value)
    {
        return value
            .Replace("\r\n", " ")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\"", "\\\"");
    }

}
