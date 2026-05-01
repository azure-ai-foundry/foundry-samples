// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Storage.Transcript;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class TestUtilities
    {
        private static string rootFolder = PathUtils.NormalizePath(@"..\..\..");

        public static TurnContext CreateEmptyContext()
        {
            var b = new TestAdapter();
            var a = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = "EmptyContext",
                From = new ChannelAccount
                {
                    Id = "empty@empty.context.org",
                },

                Conversation = new ConversationAccount()
                {
                    Id = "213123123123",
                },
            };
            var bc = new TurnContext(b, a);

            return bc;
        }

        public static IEnumerable<object[]> GetTestScripts(string relativeFolder)
        {
            string testFolder = Path.GetFullPath(Path.Combine(rootFolder, PathUtils.NormalizePath(relativeFolder)));
            return Directory.EnumerateFiles(testFolder, "*.test.dialog", SearchOption.AllDirectories).Select(s => new object[] { Path.GetFileName(s) }).ToArray();
        }
    }
}
