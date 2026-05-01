// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using Xunit.Abstractions;

namespace Microsoft.Agents.Auth.Tests
{
    public class TraceConsoleLoggingProvider : ILoggerProvider
    {
        protected ITestOutputHelper _output { get; }
        private readonly ConcurrentDictionary<string, ILogger> _loggers = new ConcurrentDictionary<string, ILogger>();

        public TraceConsoleLoggingProvider(ITestOutputHelper output)
        {
            _output = output;
        }

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, name => XUnitLogger.CreateLogger(_output, name, LogLevel.Trace, null));

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public class XUnitLogger : ILogger
    {
        protected IExternalScopeProvider _scopeProvider { get; } = new LoggerExternalScopeProvider();

        protected string _categoryName = string.Empty;

        protected ITestOutputHelper _output = null;

        protected LogLevel? _level = null;

        public static ILogger CreateLogger(
           ITestOutputHelper output,
           string name = null,
           LogLevel? level = null,
           IExternalScopeProvider scopeProvider = null) => new XUnitLogger(output, name, level, scopeProvider);

        protected XUnitLogger(
            ITestOutputHelper output,
            string categoryName = null,
            LogLevel? level = LogLevel.Trace,
            IExternalScopeProvider scopeProvider = null)
        {
            _scopeProvider = scopeProvider ?? new LoggerExternalScopeProvider();
            _categoryName = categoryName;
            _output = output;
            _level = level;
        }


        public IDisposable BeginScope<TState>(TState state) => _scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _level;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            string message = formatter(state, exception);
            _output.WriteLine($"{logLevel}=> {message}");

        }
    }
}
