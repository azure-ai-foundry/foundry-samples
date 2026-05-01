namespace Microsoft.Agents.Builder.Telemetry.Authorization
{
    /// <summary>
    /// Defines the <see cref="System.Diagnostics.Activity"/> names used by the authorization telemetry scopes.
    /// </summary>
    internal static class Constants
    {
        /// <summary>Activity name for acquiring an agentic token during authorization.</summary>
        internal static readonly string ScopeAgenticToken = "agents.authorization.agentic_token";

        /// <summary>Activity name for acquiring an Azure Bot Framework user token.</summary>
        internal static readonly string ScopeAzureBotToken = "agents.authorization.azure_bot_token";

        /// <summary>Activity name for signing a user in via Azure Bot Framework OAuth.</summary>
        internal static readonly string ScopeAzureBotSignIn = "agents.authorization.azure_bot_signin";

        /// <summary>Activity name for signing a user out via Azure Bot Framework OAuth.</summary>
        internal static readonly string ScopeAzureBotSignOut = "agents.authorization.azure_bot_signout";
    }
}