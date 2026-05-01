// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> for
    /// core model types not handled by a registered custom <see cref="System.Text.Json.Serialization.JsonConverter"/>.
    /// Wired into <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer.SerializationOptions"/> as the base of the
    /// <see cref="System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver"/> chain.
    /// </summary>
    /// <remarks>
    /// Types handled by converters registered in <c>ApplyCoreOptions()</c> are intentionally
    /// excluded — including them would produce source-gen warnings and could bypass converter
    /// logic for callers who access <c>GetTypeInfo()</c> directly.
    /// </remarks>
    [JsonSourceGenerationOptions(
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        IncludeFields = true)]
    // --- Concrete model types not handled by a custom converter ---
    [JsonSerializable(typeof(ActivityTreatment))]
    [JsonSerializable(typeof(AdaptiveCardInvokeAction))]
    [JsonSerializable(typeof(AdaptiveCardInvokeResponse))]
    [JsonSerializable(typeof(AdaptiveCardInvokeValue))]
    [JsonSerializable(typeof(AadResourceUrls))]
    [JsonSerializable(typeof(AIEntity))]
    [JsonSerializable(typeof(Attachment))]
    [JsonSerializable(typeof(AudioCard))]
    [JsonSerializable(typeof(BasicCard))]
    [JsonSerializable(typeof(CardImage))]
    [JsonSerializable(typeof(ChannelAccount))]
    [JsonSerializable(typeof(Citation))]
    [JsonSerializable(typeof(CommandResultValue<JsonElement>))]
    [JsonSerializable(typeof(CommandValue<JsonElement>))]
    [JsonSerializable(typeof(ConversationAccount))]
    [JsonSerializable(typeof(ConversationParameters))]
    [JsonSerializable(typeof(ConversationReference))]
    [JsonSerializable(typeof(Error))]
    [JsonSerializable(typeof(ExpectedReplies))]
    [JsonSerializable(typeof(Fact))]
    [JsonSerializable(typeof(GeoCoordinates))]
    [JsonSerializable(typeof(HeroCard))]
    [JsonSerializable(typeof(InnerHttpError))]
    [JsonSerializable(typeof(InvokeResponse))]
    [JsonSerializable(typeof(MediaCard))]
    [JsonSerializable(typeof(MediaEventValue))]
    [JsonSerializable(typeof(MediaUrl))]
    [JsonSerializable(typeof(Mention))]
    [JsonSerializable(typeof(MessageReaction))]
    [JsonSerializable(typeof(OAuthCard))]
    [JsonSerializable(typeof(PagedMembersResult))]
    [JsonSerializable(typeof(Place))]
    [JsonSerializable(typeof(ProductInfo))]
    [JsonSerializable(typeof(ReceiptCard))]
    [JsonSerializable(typeof(ReceiptItem))]
    [JsonSerializable(typeof(ResourceResponse))]
    [JsonSerializable(typeof(SearchInvokeOptions))]
    [JsonSerializable(typeof(SearchInvokeResponse))]
    [JsonSerializable(typeof(SearchInvokeValue))]
    [JsonSerializable(typeof(SemanticAction))]
    [JsonSerializable(typeof(SignInResource))]
    [JsonSerializable(typeof(SigninCard))]
    [JsonSerializable(typeof(StreamInfo))]
    [JsonSerializable(typeof(SuggestedActions))]
    [JsonSerializable(typeof(TextHighlight))]
    [JsonSerializable(typeof(Thing))]
    [JsonSerializable(typeof(TokenExchangeInvokeRequest))]
    [JsonSerializable(typeof(TokenExchangeInvokeResponse))]
    [JsonSerializable(typeof(TokenExchangeRequest))]
    [JsonSerializable(typeof(TokenExchangeResource))]
    [JsonSerializable(typeof(TokenExchangeState))]
    [JsonSerializable(typeof(TokenOrSignInResourceResponse))]
    [JsonSerializable(typeof(TokenPollingSettings))]
    [JsonSerializable(typeof(TokenPostResource))]
    [JsonSerializable(typeof(TokenRequest))]
    [JsonSerializable(typeof(TokenResponse))]
    [JsonSerializable(typeof(TokenStatus))]
    [JsonSerializable(typeof(VideoCard))]
    // --- Common collection types ---
    [JsonSerializable(typeof(List<Attachment>))]
    [JsonSerializable(typeof(List<CardAction>))]
    [JsonSerializable(typeof(List<ChannelAccount>))]
    [JsonSerializable(typeof(IReadOnlyList<ChannelAccount>))]
    [JsonSerializable(typeof(List<ConversationParameters>))]
    [JsonSerializable(typeof(List<MessageReaction>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(Dictionary<string, JsonElement>))]
    public sealed partial class CoreJsonContext : JsonSerializerContext
    {
    }
}
