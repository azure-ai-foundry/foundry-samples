// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Connector.Errors
{
    /// <summary>
    /// Error helper for the Agent SDK core system
    /// This is used to setup the localized error codes for the AgentSDK
    /// 
    /// Each Error should be created as as an AgentAuthErrorDefinition and added to the ErrorHelper class
    /// Each definition should include an error code as a - from the base error code, a description sorted in the Resource.resx file to support localization, and a help link pointing to an AKA link to get help for the given error. 
    /// 
    /// 
    /// when used, there are is 2 methods in used in the general space. 
    /// Method 1: 
    /// Throw a new exception with the error code, description and helplink
    ///     throw new IndexOutOfRangeException(ErrorHelper.MissingAuthenticationConfiguration.description)
    ///     {
    ///         HResult = ErrorHelper.MissingAuthenticationConfiguration.code,
    ///         HelpLink = ErrorHelper.MissingAuthenticationConfiguration.helplink
    ///     };
    ///
    /// Method 2: 
    /// 
    ///     throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException&lt;OperationCanceledException&gt;(
    ///         ErrorHelper.NullIAccessTokenProvider, ex, $"{AgentClaims.GetAppId(claimsIdentity)}:{serviceUrl}");
    /// 
    /// </summary>
    internal static partial class ErrorHelper
    {
        // Base error code for the connector: -50500

        internal static readonly AgentErrorDefinition InvalidAccessTokenForAgentCallback = new AgentErrorDefinition(-50500, Properties.Resources.ConversationResponseUnAuthorized, "https://aka.ms/M365AgentsErrorCodes/#-50500");
        internal static readonly AgentErrorDefinition SendGetConversationsError = new AgentErrorDefinition(-50501, Properties.Resources.GetConversationsError, "https://aka.ms/M365AgentsErrorCodes/#-50501");
        internal static readonly AgentErrorDefinition SendCreateConversationError = new AgentErrorDefinition(-50502, Properties.Resources.CreateConversationError, "https://aka.ms/M365AgentsErrorCodes/#-50502");
        internal static readonly AgentErrorDefinition SendSendConversationError = new AgentErrorDefinition(-50503, Properties.Resources.SendToConversationError, "https://aka.ms/M365AgentsErrorCodes/#-50503");
        internal static readonly AgentErrorDefinition SendConversationHistoryError = new AgentErrorDefinition(-50504, Properties.Resources.SendConversationHistoryError, "https://aka.ms/M365AgentsErrorCodes/#-50504");
        internal static readonly AgentErrorDefinition SendUpdateActivityError = new AgentErrorDefinition(-50505, Properties.Resources.SendUpdateActivityError, "https://aka.ms/M365AgentsErrorCodes/#-50505");
        internal static readonly AgentErrorDefinition SendReplyToActivityError = new AgentErrorDefinition(-50506, Properties.Resources.ReplyToActivityError, "https://aka.ms/M365AgentsErrorCodes/#-50506");
        internal static readonly AgentErrorDefinition SendDeleteActivityError = new AgentErrorDefinition(-50507, Properties.Resources.SendDeleteActivity, "https://aka.ms/M365AgentsErrorCodes/#-50507");
        internal static readonly AgentErrorDefinition SendGetConversationMembersError = new AgentErrorDefinition(-50508, Properties.Resources.SendGetConversationMembers, "https://aka.ms/M365AgentsErrorCodes/#-50508");
        internal static readonly AgentErrorDefinition SendGetConversationMemberError = new AgentErrorDefinition(-50509, Properties.Resources.SendGetConversationMember, "https://aka.ms/M365AgentsErrorCodes/#-50509");
        internal static readonly AgentErrorDefinition SendDeleteConversationMemberError = new AgentErrorDefinition(-50510, Properties.Resources.SendDeleteConversationMember, "https://aka.ms/M365AgentsErrorCodes/#-50510");
        internal static readonly AgentErrorDefinition SendGetConversationPagedMembersError = new AgentErrorDefinition(-50511, Properties.Resources.SendGetConversationPagedMembers, "https://aka.ms/M365AgentsErrorCodes/#-50511");
        internal static readonly AgentErrorDefinition SendGetActivityMembersError = new AgentErrorDefinition(-50512, Properties.Resources.SendGetActivityMembers, "https://aka.ms/M365AgentsErrorCodes/#-50512");
        internal static readonly AgentErrorDefinition SendUploadAttachmentError = new AgentErrorDefinition(-50513, Properties.Resources.SendUploadAttachment, "https://aka.ms/M365AgentsErrorCodes/#-50513");
        internal static readonly AgentErrorDefinition GetSignInResourceAsync_BadRequestError = new AgentErrorDefinition(-50514, Properties.Resources.GetSignInResourceAsync_BadRequest, "https://aka.ms/M365AgentsErrorCodes/#-50514");
        internal static readonly AgentErrorDefinition GetAttachmentError = new AgentErrorDefinition(-50515, Properties.Resources.GetAttachment_Error, "https://aka.ms/M365AgentsErrorCodes/#-50515");
        internal static readonly AgentErrorDefinition GetAttachmentInfoError = new AgentErrorDefinition(-50516, Properties.Resources.GetAttachmentInfoError, "https://aka.ms/M365AgentsErrorCodes/#-50516");
        internal static readonly AgentErrorDefinition TokenServiceExchangeFailed = new AgentErrorDefinition(-50517, Properties.Resources.TokenServiceExchangeFailed, "https://aka.ms/M365AgentsErrorCodes/#-50517");
        internal static readonly AgentErrorDefinition TokenServiceExchangeUnexpected = new AgentErrorDefinition(-50518, Properties.Resources.TokenServiceExchangeUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-50518");
        internal static readonly AgentErrorDefinition TokenServiceGetTokenUnexpected = new AgentErrorDefinition(-50519, Properties.Resources.TokenServiceGetTokenUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-50519");
        internal static readonly AgentErrorDefinition TokenServiceGetAadTokenUnexpected = new AgentErrorDefinition(-50520, Properties.Resources.TokenServiceGetAadTokenUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-50520");
        internal static readonly AgentErrorDefinition TokenServiceSignOutUnexpected = new AgentErrorDefinition(-50521, Properties.Resources.TokenServiceSignOutUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-50521");
        internal static readonly AgentErrorDefinition TokenServiceGetTokenStatusUnexpected = new AgentErrorDefinition(-50522, Properties.Resources.TokenServiceGetTokenStatusUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-50522");
        internal static readonly AgentErrorDefinition TokenServiceGetTokenOrSignInResourceUnexpected = new AgentErrorDefinition(-50523, Properties.Resources.TokenServiceGetTokenOrSignInResourceUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-50523");
        internal static readonly AgentErrorDefinition TokenServiceGetSignInUrlUnexpected = new AgentErrorDefinition(-50524, Properties.Resources.TokenServiceGetSignInUrlUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-50524");
        internal static readonly AgentErrorDefinition TokenServiceGetSignInResourceUnexpected = new AgentErrorDefinition(-50525, Properties.Resources.TokenServiceGetSignInResourceUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-50525");
        internal static readonly AgentErrorDefinition TokenServiceExchangeErrorResponse = new AgentErrorDefinition(-50526, Properties.Resources.TokenServiceExchangeErrorResponse, "https://aka.ms/M365AgentsErrorCodes/#-50526");
        internal static readonly AgentErrorDefinition TeamsTargetedRequiresGroupChat = new AgentErrorDefinition(-50527, Properties.Resources.TeamsTargetedRequiresGroupChat, "https://aka.ms/M365AgentsErrorCodes/#-50527");
    }
}
