using System.Diagnostics;
using System;
using Xunit;
using System.IO;

namespace Microsoft.Agents.Storage.Tests
{
    public sealed class IgnoreOnNoEmulatorFact : FactAttribute
    {
        private const string NoEmulatorMessage = "This test requires CosmosDB Emulator! go to https://aka.ms/documentdb-emulator-docs to download and install.";

        public static readonly Lazy<bool> HasEmulator = new Lazy<bool>(() =>
        {
            //if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_NAME")))
            //{
            //    return false;
            //}

            var enabled = Environment.GetEnvironmentVariable("XUNITCOSMOSDBTESTENABLED");
            if (string.IsNullOrWhiteSpace(enabled) || enabled != "1")
            {
                return false;
            }

            if (!File.Exists(CosmosDbConstants.EmulatorPath))
            {
                return false;
            }

            /*
            // Verify the emulator runs
            var p = new Process
            {
                StartInfo =
                {
                    UseShellExecute = true,
                    FileName = EmulatorPath,
                    Arguments = "/GetStatus",
                },
            };
            p.Start();
            p.WaitForExit();

            return p.ExitCode == 2;
            */

            return true;
        });

        public IgnoreOnNoEmulatorFact()
        {
            if (!HasEmulator.Value)
            {
                Skip = "This test requires CosmosDB Emulator! go to https://aka.ms/documentdb-emulator-docs to download and install.";
            }

            if (Debugger.IsAttached)
            {
                Assert.True(HasEmulator.Value, NoEmulatorMessage);
            }
        }
    }
}