using Microsoft.Agents.Builder.Dialogs;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework("Microsoft.Agents.Builder.Startup", "Microsoft.Agents.Builder.Dialogs.Tests")]

namespace Microsoft.Agents.Builder
{
    public class Startup : XunitTestFramework
    {
        public Startup(IMessageSink messageSink)
            : base(messageSink)
        {
            //ComponentRegistration.Add(new DialogsComponentRegistration());
        }
    }
}
