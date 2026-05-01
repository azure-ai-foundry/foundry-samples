// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Microsoft.Agents.Core.Telemetry
{
    public static class ActivityExtensions
    {
        /// <summary>
        /// Creates a new <see cref="System.Diagnostics.Activity"/> that clones the specified source activity.
        /// This is intended for scenarios such as cross-thread telemetry propagation, where
        /// a new activity instance is needed while preserving correlation with the original.
        /// </summary>
        /// <param name="source">
        /// The source <see cref="System.Diagnostics.Activity"/> to clone. Its operation name, ID format, parent ID (if any),
        /// tags, baggage, links, events, and start time are copied to the cloned activity.
        /// </param>
        /// <param name="start">
        /// If <c>true</c>, the cloned activity is started by calling <see cref="System.Diagnostics.Activity.Start()"/>;
        /// if <c>false</c>, the cloned activity is returned in a non-started state.
        /// </param>
        /// <returns>
        /// A new <see cref="System.Diagnostics.Activity"/> instance that mirrors the source activity's metadata while
        /// preserving its logical parent-child relationship via the copied parent ID and links.
        /// The returned activity is independent of the source and has its own lifecycle.
        /// </returns>
        public static Activity? CloneActivity(this Activity source, bool start = false)
        {
            Activity? clone;
            if (source.Parent?.Context != null)
            {
                clone = source.Source.CreateActivity(source.OperationName, source.Kind, source.Parent.Context);
            }
            else
            {
                clone = source.Source.CreateActivity(source.OperationName, source.Kind);
            }

            if (clone == null) return null;

            clone.SetIdFormat(source.IdFormat);

            if (source.ParentId != null)
            {
                clone.SetParentId(source.TraceId, source.SpanId);
            }

            foreach (var tag in source.Tags)
            {
                clone.AddTag(tag.Key, tag.Value);
            }

            foreach (var bag in source.Baggage)
            {
                clone.AddBaggage(bag.Key, bag.Value);
            }

            foreach (var link in source.Links)
            {
                clone.AddLink(link);
            }

            foreach (var eventItem in source.Events)
            {
                clone.AddEvent(eventItem);
            }

            if (start)
            {
                clone.Start();
            }

            return clone;
        }
    }
}
