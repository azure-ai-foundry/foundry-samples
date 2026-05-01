// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage.Transcript;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Storage.Tests
{
    [CollectionDefinition("Non-Parallel Collection", DisableParallelization = true)]
    [Collection("Non-Parallel Collection")]
    public class FileTranscriptTests : TranscriptBaseTests
    {
        public FileTranscriptTests()
            : base()
        {
            this.Store = new FileTranscriptLogger(Folder);
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, true);
            }
        }

        public static string Folder
        {
            get { return Path.Combine(nameof(FileTranscriptTests)); }
        }

        [Fact]
        public async Task FileTranscript_BadArgs()
        {
            await BadArgs();
        }

        [Fact]
        public async Task FileTranscript_LogActivity()
        {
            await LogActivity();
        }

        [Fact]
        public async Task FileTranscript_LogActivityWithInvalidIds()
        {
            await LogActivityWithInvalidIds();
        }

        [Fact]
        public async Task FileTranscript_LogMultipleActivities()
        {
            await LogMultipleActivities();
        }

        [Fact]
        public async Task FileTranscript_LogActivitiesShouldCatchException()
        {
            await LogActivitiesShouldCatchException();
        }

        [Fact]
        public async Task FileTranscript_GetConversationActivities()
        {
            await GetTranscriptActivities();
        }

        [Fact]
        public async Task FileTranscript_GetConversationActivitiesStartDate()
        {
            await GetTranscriptActivitiesStartDate();
        }

        [Fact]
        public async Task FileTranscript_ListConversations()
        {
            await ListTranscripts();
        }

        [Fact]
        public async Task FileTranscript_DeleteConversation()
        {
            await DeleteTranscript();
        }
    }
}
