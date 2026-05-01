// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Microsoft.Agents.Core.Telemetry.Tests
{

    [Collection("TelemetryTests")]
    public class TelemetryScopeTests : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly List<Activity> _startedActivities = new List<Activity>();
        private readonly List<Activity> _stoppedActivities = new List<Activity>();

        public TelemetryScopeTests()
        {
            // Register a listener so that Activities are actually created
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == AgentsTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => _startedActivities.Add(activity),
                ActivityStopped = activity => _stoppedActivities.Add(activity)
            };
            ActivitySource.AddActivityListener(_listener);
            _startedActivities.Clear();
            _stoppedActivities.Clear();
        }

        public void Dispose()
        {
            _listener.ActivityStarted = null;
            _listener.ActivityStopped = null;
            _listener.Dispose();
        }

        [Fact]
        public void TelemetryScope_CreatesActivity_WithCorrectName()
        {
            using var scope = new TelemetryScope("TestOperation");

            Assert.Single(_startedActivities);
            Assert.Equal("TestOperation", _startedActivities[0].OperationName);
        }

        [Fact]
        public void TelemetryScope_DefaultKind_IsInternal()
        {
            using var scope = new TelemetryScope("TestOperation");

            Assert.Single(_startedActivities);
            Assert.Equal(ActivityKind.Internal, _startedActivities[0].Kind);
        }

        [Fact]
        public void TelemetryScope_CustomKind_IsRespected()
        {
            using var scope = new TelemetryScope("TestOperation", ActivityKind.Client);

            Assert.Single(_startedActivities);
            Assert.Equal(ActivityKind.Client, _startedActivities[0].Kind);
        }

        [Theory]
        [InlineData(ActivityKind.Server)]
        [InlineData(ActivityKind.Producer)]
        [InlineData(ActivityKind.Consumer)]
        public void TelemetryScope_AllActivityKinds_AreRespected(ActivityKind kind)
        {
            using var scope = new TelemetryScope("TestOperation", kind);

            Assert.Single(_startedActivities);
            Assert.Equal(kind, _startedActivities[0].Kind);
        }

        [Fact]
        public void SetError_SetsActivityStatusToError()
        {
            var scope = new TelemetryScope("TestOperation");
            var exception = new InvalidOperationException("test error");

            scope.SetError(exception);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("test error", stopped.StatusDescription);
        }

        [Fact]
        public void SetError_AddsExceptionEvent()
        {
            var scope = new TelemetryScope("TestOperation");
            var exception = new InvalidOperationException("test error");

            scope.SetError(exception);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var exceptionEvent = stopped.Events.FirstOrDefault(e => e.Name == "exception");
            Assert.Equal("exception", exceptionEvent.Name);
            Assert.Equal(typeof(InvalidOperationException).FullName, exceptionEvent.Tags.First(t => t.Key == "exception.type").Value);
            Assert.Equal("test error", exceptionEvent.Tags.First(t => t.Key == "exception.message").Value);
        }

        [Fact]
        public void SetError_AddsExceptionStackTrace()
        {
            var scope = new TelemetryScope("TestOperation");
            Exception exception;
            try
            {
                throw new InvalidOperationException("stack trace test");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            scope.SetError(exception);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var exceptionEvent = stopped.Events.FirstOrDefault(e => e.Name == "exception");
            var stackTrace = exceptionEvent.Tags.First(t => t.Key == "exception.stacktrace").Value as string;
            Assert.NotNull(stackTrace);
            Assert.Contains("stack trace test", exception.Message);
        }

        [Fact]
        public void SetError_CalledMultipleTimes_AddsMultipleEvents()
        {
            var scope = new TelemetryScope("TestOperation");
            var ex1 = new InvalidOperationException("first error");
            var ex2 = new ArgumentException("second error");

            scope.SetError(ex1);
            scope.SetError(ex2);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var exceptionEvents = stopped.Events.Where(e => e.Name == "exception").ToList();
            Assert.Equal(2, exceptionEvents.Count);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("second error", stopped.StatusDescription);
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes_WithoutError()
        {
            var scope = new TelemetryScope("TestOperation");
            scope.Dispose();
            scope.Dispose(); // Should not throw
        }

        [Fact]
        public void Callback_IsInvokedOnDispose_WithActivity()
        {
            var scope = new TestTelemetryScope("CallbackTest");
            Assert.False(scope.CallbackInvoked);

            scope.Dispose();

            Assert.True(scope.CallbackInvoked);
            Assert.NotNull(scope.CapturedActivity);
            Assert.Equal("CallbackTest", scope.CapturedActivity.OperationName);
        }

        [Fact]
        public void Callback_ReceivesDuration()
        {
            var scope = new TestTelemetryScope("DurationTest");

            scope.Dispose();

            Assert.True(scope.CallbackInvoked);
            Assert.True(scope.CapturedDuration >= 0, "Duration should be non-negative.");
        }

        [Fact]
        public void Callback_ReceivesNullException_WhenNoError()
        {
            var scope = new TestTelemetryScope("NoErrorTest");

            scope.Dispose();

            Assert.True(scope.CallbackInvoked);
            Assert.Null(scope.CapturedException);
        }

        [Fact]
        public void Callback_ReceivesException_WhenErrorIsSet()
        {
            var scope = new TestTelemetryScope("ErrorTest");
            var exception = new InvalidOperationException("callback error");
            scope.SetError(exception);

            scope.Dispose();

            Assert.True(scope.CallbackInvoked);
            Assert.NotNull(scope.CapturedException);
            Assert.Same(exception, scope.CapturedException);
        }

        [Fact]
        public void Callback_IsInvokedOnlyOnce_WhenDisposedMultipleTimes()
        {
            var scope = new TestTelemetryScope("SingleCallbackTest");

            scope.Dispose();
            scope.Dispose();

            Assert.Equal(1, scope.CallbackInvokedCount);
        }

        [Fact]
        public void Callback_CanSetTagsOnActivity()
        {
            var scope = new TaggingTelemetryScope("TagTest");

            scope.Dispose();

            Assert.Equal("tag_value", scope.CapturedActivity?.Tags.FirstOrDefault(t => t.Key == "custom.tag").Value);
        }

        [Fact]
        public void Wrap_Action_ExecutesSuccessfully()
        {
            using var scope = new TestTelemetryScope("WrapActionTest");
            bool actionCalled = false;

            scope.Wrap(() => { actionCalled = true; });

            Assert.True(actionCalled);
            Assert.Null(scope.CapturedException);
        }

        [Fact]
        public void Wrap_Action_SetsError_OnException()
        {
            var scope = new TelemetryScope("WrapActionErrorTest");
            var exception = new InvalidOperationException("wrap action error");

            Assert.Throws<InvalidOperationException>(() =>
            {
                scope.Wrap(() => throw exception);
            });
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("wrap action error", stopped.StatusDescription);
        }

        [Fact]
        public void Wrap_Func_ReturnsValue_OnSuccess()
        {
            using var scope = new TelemetryScope("WrapFuncTest");

            var result = scope.Wrap(() => 42);

            Assert.Equal(42, result);
        }

        [Fact]
        public void Wrap_Func_SetsError_OnException()
        {
            var scope = new TelemetryScope("WrapFuncErrorTest");
            var exception = new InvalidOperationException("wrap func error");

            Assert.Throws<InvalidOperationException>(() =>
            {
                scope.Wrap<int>(() => throw exception);
            });
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
        }

        [Fact]
        public void Wrap_Func_RethrowsOriginalException()
        {
            using var scope = new TelemetryScope("WrapRethrowTest");
            var exception = new ArgumentNullException("param1");

            var thrown = Assert.Throws<ArgumentNullException>(() =>
            {
                scope.Wrap<int>(() => throw exception);
            });

            Assert.Same(exception, thrown);
        }

        [Fact]
        public async Task WrapAsync_Func_ReturnsValue_OnSuccess()
        {
            using var scope = new TelemetryScope("WrapAsyncFuncTest");

            var result = await scope.WrapAsync(async () =>
            {
                await Task.Delay(1);
                return 99;
            });

            Assert.Equal(99, result);
        }

        [Fact]
        public async Task WrapAsync_Func_SetsError_OnException()
        {
            var scope = new TelemetryScope("WrapAsyncFuncErrorTest");
            var exception = new InvalidOperationException("async func error");

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await scope.WrapAsync(async () =>
                {
                    await Task.Delay(1);
                    throw exception;
                });
            });
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("async func error", stopped.StatusDescription);
        }

        [Fact]
        public async Task WrapAsync_Func_RethrowsOriginalException()
        {
            using var scope = new TelemetryScope("WrapAsyncRethrowTest");
            var exception = new ArgumentException("async rethrow");

            var thrown = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await scope.WrapAsync(async () =>
                {
                    await Task.Delay(1);
                    throw exception;
                });
            });

            Assert.Same(exception, thrown);
        }

        [Fact]
        public void Wrap_Action_DoesNotSetError_OnSuccess()
        {
            var scope = new TestTelemetryScope("WrapNoErrorTest");

            scope.Wrap(() => { });
            scope.Dispose();

            Assert.Null(scope.CapturedException);
        }

        [Fact]
        public void Callback_ReceivesLastException_WhenMultipleErrorsSet()
        {
            var scope = new TestTelemetryScope("MultiErrorCallbackTest");
            var ex1 = new InvalidOperationException("first");
            var ex2 = new ArgumentException("second");

            scope.SetError(ex1);
            scope.SetError(ex2);
            scope.Dispose();

            Assert.Same(ex2, scope.CapturedException);
        }

        [Fact]
        public void Dispose_StopsActivity()
        {
            var scope = new TelemetryScope("StopTest");
            Assert.Empty(_stoppedActivities);

            scope.Dispose();

            Assert.Single(_stoppedActivities);
            Assert.Equal("StopTest", _stoppedActivities[0].OperationName);
        }

        [Fact]
        public void Dispose_StopsActivityExactlyOnce_WhenCalledMultipleTimes()
        {
            var scope = new TelemetryScope("StopOnceTest");

            scope.Dispose();
            scope.Dispose();

            Assert.Single(_stoppedActivities);
        }

        private class TestTelemetryScope : TelemetryScope
        {
            public bool CallbackInvoked { get; private set; }
            public int CallbackInvokedCount { get; private set; }
            public Activity? CapturedActivity { get; private set; }
            public double CapturedDuration { get; private set; }
            public Exception? CapturedException { get; private set; }

            public TestTelemetryScope(string activityName) : base(activityName)
            {
            }

            protected override void Callback(Activity activity, double duration, Exception? exception)
            {
                CallbackInvoked = true;
                CallbackInvokedCount++;
                CapturedActivity = activity;
                CapturedDuration = duration;
                CapturedException = exception;
            }
        }

        private class TaggingTelemetryScope : TelemetryScope
        {
            public Activity? CapturedActivity { get; private set; }

            public TaggingTelemetryScope(string activityName) : base(activityName)
            {
            }

            protected override void Callback(Activity activity, double duration, Exception? exception)
            {
                CapturedActivity = activity;
                activity.SetTag("custom.tag", "tag_value");
            }
        }
    }
}
