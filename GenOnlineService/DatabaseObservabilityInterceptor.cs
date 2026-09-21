/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
*/

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GenOnlineService
{
    // Records database metrics without SQL or parameters.
    public sealed class DatabaseObservabilityInterceptor : DbCommandInterceptor
    {
        private readonly ILogger<DatabaseObservabilityInterceptor> _logger;
        private readonly TimeSpan _slowCommandThreshold;

        public DatabaseObservabilityInterceptor(
            IConfiguration configuration,
            ILogger<DatabaseObservabilityInterceptor> logger)
        {
            _logger = logger;
            double thresholdMilliseconds =
                configuration.GetValue<double?>("OpenTelemetry:SlowDatabaseCommandMilliseconds") ?? 500;
            _slowCommandThreshold = TimeSpan.FromMilliseconds(Math.Max(1, thresholdMilliseconds));
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            Record("reader", "success", eventData.Duration);
            return result;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            Record("reader", "success", eventData.Duration);
            return ValueTask.FromResult(result);
        }

        public override object? ScalarExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result)
        {
            Record("scalar", "success", eventData.Duration);
            return result;
        }

        public override ValueTask<object?> ScalarExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result,
            CancellationToken cancellationToken = default)
        {
            Record("scalar", "success", eventData.Duration);
            return ValueTask.FromResult(result);
        }

        public override int NonQueryExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result)
        {
            Record("non_query", "success", eventData.Duration);
            return result;
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            Record("non_query", "success", eventData.Duration);
            return ValueTask.FromResult(result);
        }

        public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
        {
            Record(GetOperation(eventData.ExecuteMethod), "error", eventData.Duration);
        }

        public override Task CommandFailedAsync(
            DbCommand command,
            CommandErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Record(GetOperation(eventData.ExecuteMethod), "error", eventData.Duration);
            return Task.CompletedTask;
        }

        private void Record(string operation, string outcome, TimeSpan duration)
        {
            AppMetrics.RecordDatabaseCommand(operation, outcome, duration);
            if (duration >= _slowCommandThreshold)
            {
                _logger.LogWarning(
                    "Slow database {DatabaseOperation} command completed in {ElapsedMilliseconds} ms with outcome {Outcome}",
                    operation,
                    duration.TotalMilliseconds,
                    outcome);
            }
        }

        private static string GetOperation(DbCommandMethod method)
        {
            return method switch
            {
                DbCommandMethod.ExecuteReader => "reader",
                DbCommandMethod.ExecuteScalar => "scalar",
                DbCommandMethod.ExecuteNonQuery => "non_query",
                _ => "command"
            };
        }
    }
}
