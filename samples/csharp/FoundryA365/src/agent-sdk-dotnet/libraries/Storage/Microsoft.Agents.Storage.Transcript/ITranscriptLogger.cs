// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;

namespace Microsoft.Agents.Storage.Transcript
{
    /// <summary>
    /// Transcript logger stores activities for conversations for recall.
    /// </summary>
    public interface ITranscriptLogger
    {
        /// <summary>
        /// Log an activity to the transcript.
        /// </summary>
        /// <param name="activity">The activity to transcribe.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task LogActivityAsync(IActivity activity);
    }
}
