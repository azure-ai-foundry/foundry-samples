
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;

namespace Microsoft.Agents.Builder.Tests.App.TestUtils
{
    internal sealed class TestUserAuthenticationFeature : UserAuthorization
    {
        public TestUserAuthenticationFeature(AgentApplication app, UserAuthorizationOptions options) : base(app, options)
        {
        }
    }
}
