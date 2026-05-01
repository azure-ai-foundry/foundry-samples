using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams
{
    /// <summary>
    /// These URLs are for proactive messages only. Use these only if the incoming request serviceURL is unavailable.
    /// Once a serviceUrl has been returned from a prior conversation, you should cache and use that instead of thease. 
    /// </summary>
    public static class TeamsProactiveServiceEndpoints
    {
        public static readonly string publicGlobal = "https://smba.trafficmanager.net/teams/";
        public static readonly string gcc = "https://smba.infra.gcc.teams.microsoft.com/teams";
        public static readonly string gccHigh = "https://smba.infra.gov.teams.microsoft.us/teams";
        public static readonly string dod = "https://smba.infra.dod.teams.microsoft.us/teams";
    }
}
