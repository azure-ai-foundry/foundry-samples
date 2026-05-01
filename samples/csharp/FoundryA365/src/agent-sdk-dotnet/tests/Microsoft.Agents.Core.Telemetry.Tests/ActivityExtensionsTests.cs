// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;

#nullable enable

namespace Microsoft.Agents.Core.Telemetry.Tests
{
    [Collection("TelemetryTests")]
    public class ActivityExtensionsTests : IDisposable
    {
        private readonly ActivityListener _listener;

        public ActivityExtensionsTests()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == AgentsTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose()
        {
            _listener.ActivityStarted = null;
            _listener.ActivityStopped = null;
            _listener.Dispose();
        }

        [Fact]
        public void CloneActivity_ReturnsNull_WhenSourceCreatesNoActivity()
        {
            using var source = AgentsTelemetry.ActivitySource.CreateActivity("TestOp", ActivityKind.Internal);
            Assert.NotNull(source);
            source!.Start();

            // Dispose the listener after creating the source activity so that
            // source.Source.CreateActivity(...) returns null during clone-time.
            _listener.ActivityStarted = null;
            _listener.ActivityStopped = null;
            _listener.Dispose();

            var clone = source.CloneActivity();

            Assert.Null(clone);
        }

        [Fact]
        public void CloneActivity_ReturnsNotStarted_ByDefault()
        {
            using var source = AgentsTelemetry.ActivitySource.CreateActivity("TestOp", ActivityKind.Internal);
            Assert.NotNull(source);
            source!.Start();

            var clone = source.CloneActivity(start: false);

            Assert.NotNull(clone);
            Assert.Equal(ActivityStatusCode.Unset, clone!.Status);
            Assert.Equal(TimeSpan.Zero, clone.Duration);
            clone.Dispose();
        }

        [Fact]
        public void CloneActivity_StartsClone_WhenStartIsTrue()
        {
            using var source = AgentsTelemetry.ActivitySource.CreateActivity("StartedOp", ActivityKind.Client);
            Assert.NotNull(source);
            source!.Start();

            var clone = source.CloneActivity(start: true);

            Assert.NotNull(clone);
            Assert.Equal(ActivityStatusCode.Unset, clone!.Status);
            // A started activity has a valid StartTimeUtc
            Assert.NotEqual(default, clone.StartTimeUtc);
            clone.Stop();
            clone.Dispose();
        }

        [Fact]
        public void CloneActivity_CopiesOperationNameAndKind()
        {
            using var source = AgentsTelemetry.ActivitySource.CreateActivity("MyOperation", ActivityKind.Producer);
            Assert.NotNull(source);
            source!.Start();

            var clone = source.CloneActivity();

            Assert.NotNull(clone);
            Assert.Equal("MyOperation", clone!.OperationName);
            Assert.Equal(ActivityKind.Producer, clone.Kind);
            clone.Dispose();
        }

        [Fact]
        public void CloneActivity_CopiesTags()
        {
            using var source = AgentsTelemetry.ActivitySource.CreateActivity("TaggedOp", ActivityKind.Internal);
            Assert.NotNull(source);
            source!.Start();
            source.AddTag("key1", "value1");
            source.AddTag("key2", "value2");

            var clone = source.CloneActivity();

            Assert.NotNull(clone);
            var tags = clone!.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal("value1", tags["key1"]);
            Assert.Equal("value2", tags["key2"]);
            clone.Dispose();
        }

        [Fact]
        public void CloneActivity_CopiesBaggage()
        {
            using var source = AgentsTelemetry.ActivitySource.CreateActivity("BaggageOp", ActivityKind.Internal);
            Assert.NotNull(source);
            source!.Start();
            source.AddBaggage("bagKey", "bagValue");

            var clone = source.CloneActivity();

            Assert.NotNull(clone);
            Assert.Equal("bagValue", clone!.GetBaggageItem("bagKey"));
            clone.Dispose();
        }

        [Fact]
        public void CloneActivity_CopiesLinks()
        {
            using var source = AgentsTelemetry.ActivitySource.CreateActivity("LinkedOp", ActivityKind.Internal);
            Assert.NotNull(source);
            source!.Start();

            var linkedContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            source.AddLink(new ActivityLink(linkedContext));

            var clone = source.CloneActivity();

            Assert.NotNull(clone);
            Assert.Single(clone!.Links);
            Assert.Equal(linkedContext.TraceId, clone.Links.First().Context.TraceId);
            clone.Dispose();
        }

        [Fact]
        public void CloneActivity_PreservesParentId_WhenParentIdIsSet()
        {
            using var parent = AgentsTelemetry.ActivitySource.CreateActivity("ParentOp", ActivityKind.Internal);
            Assert.NotNull(parent);
            parent!.Start();

            using var child = AgentsTelemetry.ActivitySource.CreateActivity("ChildOp", ActivityKind.Internal, parent.Context);
            Assert.NotNull(child);
            child!.Start();

            var clone = child.CloneActivity();

            Assert.NotNull(clone);
            // The clone should reference the same trace
            Assert.Equal(child.TraceId, clone!.TraceId);
            clone.Dispose();
        }

        [Fact]
        public void CloneActivity_WithNoParent_DoesNotThrow()
        {
            using var source = AgentsTelemetry.ActivitySource.CreateActivity("NoParentOp", ActivityKind.Internal);
            Assert.NotNull(source);
            source!.Start();

            var exception = Record.Exception(() => source.CloneActivity());

            Assert.Null(exception);
        }
    }
}
