namespace Microsoft.Agents.Authentication.Telemetry
{
    /// <summary>
    /// Defines the <see cref="System.Diagnostics.Activity"/> and metric names used by the authentication telemetry scopes.
    /// </summary>
    internal static class Constants
    {
        /// <summary>Activity name for a generic access-token acquisition.</summary>
        internal static readonly string ScopeGetAccessToken = "agents.authentication.get_access_token";

        /// <summary>Activity name for an On-Behalf-Of token acquisition.</summary>
        internal static readonly string ScopeAcquireTokenOnBehalfOf = "agents.authentication.acquire_token_on_behalf_of";

        /// <summary>Activity name for acquiring an agentic instance token.</summary>
        internal static readonly string ScopeGetAgenticInstanceToken = "agents.authentication.get_agentic_instance_token";

        /// <summary>Activity name for acquiring an agentic user token.</summary>
        internal static readonly string ScopeGetAgenticUserToken = "agents.authentication.get_agentic_user_token";

        /// <summary>Metric name for the histogram that records token-request duration in milliseconds.</summary>
        internal static readonly string MetricTokenRequestDuration = "agents.auth.token.request.duration";

        /// <summary>Metric name for the counter of token requests made to the authentication service.</summary>
        internal static readonly string MetricTokenRequestCount = "agents.auth.token.request.count";

        /// <summary>Auth-method label used when acquiring a token via On-Behalf-Of flow.</summary>
        internal static readonly string AuthMethodOBO = "obo";

        /// <summary>Auth-method label used when acquiring an agentic instance token.</summary>
        internal static readonly string AuthMethodAgenticInstance = "agentic_instance";

        /// <summary>Auth-method label used when acquiring an agentic user token.</summary>
        internal static readonly string AuthMethodAgenticUser = "agentic_user";
    }
}