// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Connector.RestClients
{
    internal static class RestApiPaths
    {
        // Conversations
        public const string Conversations = "v3/conversations";
        public const string ConversationMembers = "v3/conversations/{0}/members";
        public const string ConversationMember = "v3/conversations/{0}/members/{1}";
        public const string ConversationActivities = "v3/conversations/{0}/activities";
        public const string ConversationActivity = "v3/conversations/{0}/activities/{1}";
        public const string ConversationHistory = "v3/conversations/{0}/activities/history";
        public const string ConversationPagedMembers = "v3/conversations/{0}/pagedmembers";
        public const string ActivityMembers = "v3/conversations/{0}/activities/{1}/members";
        public const string ConversationAttachments = "v3/conversations/{0}/attachments";

        // Attachments
        public const string AttachmentInfo = "v3/attachments/{0}";
        public const string AttachmentView = "v3/attachments/{0}/views/{1}";

        // User Token
        public const string UserToken = "api/usertoken/GetToken";
        public const string UserTokenSignOut = "api/usertoken/SignOut";
        public const string UserTokenStatus = "api/usertoken/GetTokenStatus";
        public const string UserTokenAad = "api/usertoken/GetAadTokens";
        public const string UserTokenExchange = "api/usertoken/exchange";
        public const string UserTokenOrSignInResource = "api/usertoken/GetTokenOrSignInResource";

        // Agent Sign-In
        public const string AgentSignIn = "api/botsignin/GetSignInUrl";
        public const string AgentSignInResource = "api/botsignin/GetSignInResource";
    }
}
