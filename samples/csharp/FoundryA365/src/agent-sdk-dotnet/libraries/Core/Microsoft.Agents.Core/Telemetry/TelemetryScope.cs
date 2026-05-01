// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.Agents.Core.Telemetry
{
    /// <summary>
    /// Provides a disposable scope that wraps an OpenTelemetry <see cref="System.Diagnostics.Activity"/> for
    /// tracing operations within the Microsoft Agents SDK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="TelemetryScope"/> starts a new <see cref="System.Diagnostics.Activity"/> from
    /// <see cref="Microsoft.Agents.Core.Telemetry.AgentsTelemetry.ActivitySource"/> when constructed, and stops it when
    /// disposed. Errors can be recorded via <see cref="SetError"/>, which sets the
    /// activity status to <see cref="System.Diagnostics.ActivityStatusCode.Error"/> and attaches an
    /// <c>exception</c> event following OpenTelemetry semantic conventions.
    /// </para>
    /// <para>
    /// Derived classes can override <see cref="Callback"/> to enrich the activity with
    /// additional tags or record metrics just before it is stopped.
    /// </para>
    /// <para>
    /// The <see cref="Wrap(Action)"/>, <see cref="Wrap{T}(Func{T})"/>,
    /// <see cref="WrapAsync(Func{System.Threading.Tasks.Task})"/>, and <see cref="WrapAsync{T}(Func{System.Threading.Tasks.Task{T}})"/>
    /// helper methods execute a delegate within the scope and automatically call
    /// <see cref="SetError"/> if the delegate throws.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using var scope = new TelemetryScope("MyOperation");
    /// scope.Wrap(() => DoWork());
    /// </code>
    /// </example>
    public class TelemetryScope : IDisposable
    {
        private readonly Activity? _telemetryActivity;
        private Exception? _error = null;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryScope"/> class and starts
        /// a new <see cref="System.Diagnostics.Activity"/> from <see cref="Microsoft.Agents.Core.Telemetry.AgentsTelemetry.ActivitySource"/>.
        /// </summary>
        /// <param name="activityName">The operation name for the new <see cref="System.Diagnostics.Activity"/>.</param>
        /// <param name="activityKind">
        /// The <see cref="System.Diagnostics.ActivityKind"/> for the new activity.
        /// Defaults to <see cref="System.Diagnostics.ActivityKind.Internal"/>.
        /// </param>
        public TelemetryScope(string activityName, ActivityKind activityKind = ActivityKind.Internal)
        {
            _telemetryActivity = AgentsTelemetry.ActivitySource.StartActivity(
                activityName,
                activityKind
            );
        }

        /// <summary>
        /// Records an error on the underlying <see cref="System.Diagnostics.Activity"/>.
        /// </summary>
        /// <param name="ex">The exception to record.</param>
        /// <remarks>
        /// Sets the activity status to <see cref="System.Diagnostics.ActivityStatusCode.Error"/> and adds an
        /// <c>exception</c> event with <c>exception.type</c>, <c>exception.message</c>,
        /// and <c>exception.stacktrace</c> tags, following OpenTelemetry semantic conventions.
        /// If no activity was created (e.g., no listener is registered), this method is a no-op.
        /// </remarks>
        public void SetError(Exception ex)
        {
            if (_telemetryActivity == null || _disposed)
            {
                return;
            }

            _telemetryActivity.SetStatus(ActivityStatusCode.Error, ex.Message);
            _telemetryActivity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, new()
            {
                ["exception.type"] = ex.GetType().FullName,
                ["exception.message"] = ex.Message,
                ["exception.stacktrace"] = ex.StackTrace
            }));
            _error = ex;
        }

        /// <summary>
        /// Called just before the underlying <see cref="System.Diagnostics.Activity"/> is stopped during disposal.
        /// </summary>
        /// <param name="activity">The <see cref="System.Diagnostics.Activity"/> that is about to be stopped.</param>
        /// <param name="duration">The duration of the activity in milliseconds.</param>
        /// <param name="exception">
        /// The last exception recorded via <see cref="SetError"/>, or <c>null</c> if no error occurred.
        /// </param>
        /// <remarks>
        /// Override this method in derived classes to enrich the activity with additional tags
        /// or record metrics. The base implementation does nothing.
        /// </remarks>
        protected virtual void Callback(Activity activity, double duration, Exception? exception)
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the resources used by this <see cref="TelemetryScope"/>.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release managed resources; <c>false</c> when called from a finalizer.
        /// </param>
        /// <remarks>
        /// When <paramref name="disposing"/> is <c>true</c> and an <see cref="System.Diagnostics.Activity"/> is
        /// active, <see cref="Callback"/> is invoked before the activity is stopped.
        /// Multiple calls are safe; only the first call performs cleanup.
        /// </remarks>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_telemetryActivity != null)
                    {
                        Callback(_telemetryActivity, _telemetryActivity.Duration.TotalMilliseconds, _error);
                        _telemetryActivity.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Executes the specified <paramref name="action"/> within this telemetry scope.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <remarks>
        /// If <paramref name="action"/> throws, the exception is recorded via
        /// <see cref="SetError"/> and then re-thrown.
        /// </remarks>
        public void Wrap(Action action)
        {
            Wrap(() =>
            {
                action();
                return true;
            });
        }

        /// <summary>
        /// Executes the specified <paramref name="func"/> within this telemetry scope and returns its result.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="func">The function to execute.</param>
        /// <returns>The value returned by <paramref name="func"/>.</returns>
        /// <remarks>
        /// If <paramref name="func"/> throws, the exception is recorded via
        /// <see cref="SetError"/> and then re-thrown.
        /// </remarks>
        public T Wrap<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                SetError(ex);
                throw;
            }
        }

        /// <summary>
        /// Executes the specified asynchronous <paramref name="action"/> within this telemetry scope.
        /// </summary>
        /// <param name="action">The asynchronous action to execute.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> that represents the asynchronous operation.</returns>
        /// <remarks>
        /// If <paramref name="action"/> throws, the exception is recorded via
        /// <see cref="SetError"/> and then re-thrown.
        /// </remarks>
        public async Task WrapAsync(Func<Task> action)
        {
            await WrapAsync<bool>(async () =>
            {
                await action();
                return true;
            });
        }

        /// <summary>
        /// Executes the specified asynchronous <paramref name="action"/> within this telemetry
        /// scope and returns its result.
        /// </summary>
        /// <typeparam name="T">The return type of the asynchronous function.</typeparam>
        /// <param name="action">The asynchronous function to execute.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks.Task{T}"/> that represents the asynchronous operation and contains
        /// the value returned by <paramref name="action"/>.
        /// </returns>
        /// <remarks>
        /// If <paramref name="action"/> throws, the exception is recorded via
        /// <see cref="SetError"/> and then re-thrown.
        /// </remarks>
        public async Task<T> WrapAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                SetError(ex);
                throw;
            }
        }
    }
}
