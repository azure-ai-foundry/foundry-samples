
namespace Microsoft.Agents.CopilotStudio.Client.Models
{
#if !NETSTANDARD
    public record ExecuteTurnResponse : ResponseBase
#else
    public class ExecuteTurnResponse : ResponseBase
#endif
    {
    }
}
