/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
*/

using Sentry;

namespace GenOnlineService
{
    // Sends ILogger records to Sentry Logs.
    public sealed class SentryStructuredLoggingProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly LogLevel _minimumLevel;
        private IExternalScopeProvider? _scopeProvider;

        public SentryStructuredLoggingProvider(LogLevel minimumLevel)
        {
            _minimumLevel = minimumLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new SentryStructuredLogger(categoryName, this);
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }

        public void Dispose()
        {
        }

        private sealed class SentryStructuredLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly SentryStructuredLoggingProvider _provider;

            public SentryStructuredLogger(string categoryName, SentryStructuredLoggingProvider provider)
            {
                _categoryName = categoryName;
                _provider = provider;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None
                    && logLevel >= _provider._minimumLevel
                    && !_categoryName.StartsWith("Sentry", StringComparison.Ordinal)
                    && SentrySdk.IsEnabled;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                string message = formatter(state, exception);
                List<KeyValuePair<string, object>> attributes =
                [
                    new("logger.name", _categoryName)
                ];
                AddStateAttributes(state, attributes);
                _provider._scopeProvider?.ForEachScope(
                    (scope, target) => AddStateAttributes(scope, target),
                    attributes);

                if (eventId.Id != 0)
                {
                    attributes.Add(new("event.id", eventId.Id));
                }
                if (!String.IsNullOrWhiteSpace(eventId.Name))
                {
                    attributes.Add(new("event.name", eventId.Name));
                }
                if (exception != null)
                {
                    attributes.Add(new("exception.type", exception.GetType().FullName ?? exception.GetType().Name));
                    attributes.Add(new("exception.message", exception.Message));
                    attributes.Add(new("exception.stacktrace", exception.ToString()));
                }

                void ConfigureLog(SentryLog log)
                {
                    foreach (KeyValuePair<string, object> attribute in attributes)
                    {
                        log.SetAttribute(attribute.Key, attribute.Value);
                    }
                }

                try
                {
                    switch (logLevel)
                    {
                        case LogLevel.Trace:
                            SentrySdk.Logger.LogTrace(ConfigureLog, "{0}", message);
                            break;
                        case LogLevel.Debug:
                            SentrySdk.Logger.LogDebug(ConfigureLog, "{0}", message);
                            break;
                        case LogLevel.Information:
                            SentrySdk.Logger.LogInfo(ConfigureLog, "{0}", message);
                            break;
                        case LogLevel.Warning:
                            SentrySdk.Logger.LogWarning(ConfigureLog, "{0}", message);
                            break;
                        case LogLevel.Error:
                            SentrySdk.Logger.LogError(ConfigureLog, "{0}", message);
                            break;
                        case LogLevel.Critical:
                            SentrySdk.Logger.LogFatal(ConfigureLog, "{0}", message);
                            break;
                    }
                }
                catch
                {
                    // Telemetry is fail-open.
                }
            }

            private static void AddStateAttributes(object? state, List<KeyValuePair<string, object>> attributes)
            {
                if (state is not IEnumerable<KeyValuePair<string, object?>> values)
                {
                    return;
                }

                foreach (KeyValuePair<string, object?> value in values)
                {
                    if (value.Value == null)
                    {
                        continue;
                    }

                    string name = value.Key == "{OriginalFormat}" ? "message.template" : value.Key;
                    attributes.Add(new(name, value.Value));
                }
            }
        }
    }
}
