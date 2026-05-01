// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Storage.Transcript
{
    /// <summary>
    /// Represents a transcript logger that writes activities to a Trace object.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Microsoft.Agents.Storage.Transcript.TraceTranscriptLogger"/> class.
    /// </remarks>
    /// <param name="traceActivity">Indicates if trace information should be logged.</param>
    public class TraceTranscriptLogger(bool traceActivity = true) : ITranscriptLogger
    {
        /// <summary>
        /// Log an activity to the transcript.
        /// </summary>
        /// <param name="activity">The activity to transcribe.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public Task LogActivityAsync(IActivity activity)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            if (traceActivity)
            {
                System.Diagnostics.Trace.TraceInformation(ProtocolJsonSerializer.ToJson(activity));
            }
            else
            {
                if (System.Diagnostics.Debugger.IsAttached && activity.Type == ActivityTypes.Message)
                {
                    System.Diagnostics.Trace.TraceInformation($"{activity.From.Name ?? activity.From.Id ?? activity.From.Role} [{activity.Type}] {activity.Text}");
                }
                else
                {
                    System.Diagnostics.Trace.TraceInformation($"{activity.From.Name ?? activity.From.Id ?? activity.From.Role} [{activity.Type}]");
                }
            }

            return Task.CompletedTask;
        }
    }
}
