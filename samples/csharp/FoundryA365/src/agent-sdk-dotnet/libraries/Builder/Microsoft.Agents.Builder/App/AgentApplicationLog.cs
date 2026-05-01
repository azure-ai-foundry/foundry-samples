// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Builder.App
{
    // Event ID registry for AgentApplication log messages:
    //   1 = Route evaluation order (LogRouteList)
    //   Future messages: start from 2
    public partial class AgentApplication
    {
#if !NETSTANDARD
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
            Message = "AgentApplication route evaluation order ({RouteCount} routes):\n{RouteList}")]
        private static partial void LogRouteList(ILogger logger, int routeCount, string routeList);
#else
        private static void LogRouteList(ILogger logger, int routeCount, string routeList)
        {
            logger.LogDebug("AgentApplication route evaluation order ({RouteCount} routes):\n{RouteList}",
                routeCount, routeList);
        }
#endif
    }
}
