using System.Reflection;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Moderator;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using Microsoft.Agents.Builder.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils
{
    public class TestModerator : IModerator<ITurnState>
    {
        public IList<string> Record { get; } = new List<string>();

        public Task<Plan> ReviewOutputAsync(ITurnContext turnContext, ITurnState turnState, Plan plan, CancellationToken cancellationToken = default)
        {
            Record.Add(MethodBase.GetCurrentMethod()!.Name);
            return Task.FromResult(plan);
        }

        public Task<Plan?> ReviewInputAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken = default)
        {
            Record.Add(MethodBase.GetCurrentMethod()!.Name);
            return Task.FromResult<Plan?>(null);
        }
    }
}
