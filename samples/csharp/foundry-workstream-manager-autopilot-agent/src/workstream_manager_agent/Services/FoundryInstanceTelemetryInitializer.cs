using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace WorkstreamManager.Services;

/// <summary>
/// Stamps every Application Insights telemetry item with the Foundry hosted-agent
/// identifiers that the platform injects into the container at runtime. This makes
/// telemetry sliceable per agent version and per instance, which is essential when
/// traffic routing leaves multiple agent versions active at once (draining sessions
/// stay on the previous version until they go idle).
/// </summary>
public sealed class FoundryInstanceTelemetryInitializer : ITelemetryInitializer
{
    private readonly string _agentName;
    private readonly string _agentVersion;
    private readonly string _instanceClientId;
    private readonly string _sessionId;

    public FoundryInstanceTelemetryInitializer()
    {
        _agentName = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_NAME") ?? "workstream-manager-agent";
        _agentVersion = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_VERSION") ?? "unknown";
        _instanceClientId = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_DEFAULT_INSTANCE_CLIENT_ID") ?? "unknown";
        _sessionId = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_SESSION_ID") ?? "unknown";
    }

    public void Initialize(ITelemetry telemetry)
    {
        // Native fields so the portal/Kusto expose these without custom-dimension digging.
        telemetry.Context.Cloud.RoleName = _agentName;
        telemetry.Context.Cloud.RoleInstance = _instanceClientId; // slice per instance
        telemetry.Context.Component.Version = _agentVersion;       // -> application_Version column

        // Also surface as custom dimensions for convenience in queries.
        if (telemetry is ISupportProperties props)
        {
            props.Properties["agentName"] = _agentName;
            props.Properties["agentVersion"] = _agentVersion;
            props.Properties["agentInstanceClientId"] = _instanceClientId;
            props.Properties["foundrySessionId"] = _sessionId;
        }
    }
}
