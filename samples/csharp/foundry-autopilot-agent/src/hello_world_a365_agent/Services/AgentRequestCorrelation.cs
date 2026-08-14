using System.Diagnostics;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.HeaderPropagation;

namespace HelloWorldA365.Services;

public static class AgentRequestCorrelation
{
    internal const string HeaderName = "x-ms-correlation-id";

    public static void CaptureCurrentRequest(HttpRequest request)
    {
        var activity = Activity.Current;
        if (activity?.IdFormat != ActivityIdFormat.W3C || string.IsNullOrWhiteSpace(activity.Id))
        {
            return;
        }

        // CloudAdapter automatically copies x-ms-correlation-id through its background queue.
        request.Headers[HeaderName] = activity.Id;
    }
}

public sealed class AgentRequestCorrelationMiddleware(
    ILogger<AgentRequestCorrelationMiddleware> logger) : Microsoft.Agents.Builder.IMiddleware
{
    public async Task OnTurnAsync(
        ITurnContext turnContext,
        NextDelegate next,
        CancellationToken cancellationToken = default)
    {
        var traceParent = GetTraceParent();
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            await next(cancellationToken).ConfigureAwait(false);
            return;
        }

        using var activity = new Activity("A365AgentApplication")
            .SetParentId(traceParent)
            .Start();

        logger.LogDebug(
            "Restored request correlation for agent turn. OperationId={OperationId}, ParentId={ParentId}",
            activity.TraceId,
            activity.ParentSpanId);

        try
        {
            await next(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private static string? GetTraceParent()
    {
        var headers = HeaderPropagationContext.HeadersFromRequest;
        if (headers is not null &&
            headers.TryGetValue(AgentRequestCorrelation.HeaderName, out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }
}
