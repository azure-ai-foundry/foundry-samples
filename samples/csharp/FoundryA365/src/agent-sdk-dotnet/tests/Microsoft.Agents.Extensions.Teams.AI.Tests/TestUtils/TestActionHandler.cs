using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.AI.Action;
using Microsoft.Agents.Extensions.Teams.AI.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils
{
    public class TestActionHandler : IActionHandler<ITurnState>
    {
        public string? ActionName { get; set; }

        public Task<string> PerformActionAsync(ITurnContext turnContext, ITurnState turnState, object? entities = null, string? action = null, CancellationToken cancellationToken = default)
        {
            ActionName = action;
            return Task.FromResult("test-result");
        }
    }
}
