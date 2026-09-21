# Observability

GenOnlineService sends logs, metrics, and traces with OpenTelemetry.

Send OTLP to a collector such as Grafana Alloy. The collector can route metrics to Prometheus, logs to Loki, and traces to Tempo. Sentry is optional and receives the same application logs, traces, and selected metrics.

## Configuration

Remote export is off by default:

```json
{
  "OpenTelemetry": {
    "Enabled": false,
    "OtlpEndpoint": "http://localhost:4317",
    "ServiceName": "GenOnlineService",
    "TraceSampleRatio": 1.0,
    "IncludeDatabaseStatements": false,
    "SlowDatabaseCommandMilliseconds": 500
  },
  "Sentry": {
    "enabled": false,
    "dsn": "",
    "environment": "production",
    "enable_logs": false,
    "enable_metrics": false,
    "traces_sample_rate": 0.1
  }
}
```

Production values can use environment variables:

```text
OpenTelemetry__Enabled=true
OpenTelemetry__OtlpEndpoint=http://alloy:4317
OpenTelemetry__TraceSampleRatio=0.25
Sentry__enabled=true
Sentry__dsn=https://...
Sentry__environment=production
```

`TraceSampleRatio` controls how many traces OpenTelemetry keeps. Sentry can sample those traces again with `traces_sample_rate`. Sentry keeps 10% of those traces by default (`0.1`). Raise it while validating the setup, then lower it if volume or cost is a concern.

`traces_sample_rate` only applies when `OpenTelemetry:Enabled` is also true, because Sentry receives traces through OpenTelemetry. `enable_logs` and `enable_metrics` are off by default and work independently of OpenTelemetry. Sentry gauges are sent once per cleanup tick, not on every change.

## What is collected

- Logs from `ILogger`, with trace IDs when available.
- HTTP requests and outgoing HTTP calls.
- MySQL commands and connection-pool health.
- WebSocket traffic and connection health.
- Authentication, matchmaking, lobbies, and matches.
- Background jobs and external publication.
- .NET runtime health.

Fast tick loops are not traced on every pass. That would create too much data.

## Metrics

Application metric names start with `genonline.`. Useful groups are:

- Service: online players and active lobbies.
- Authentication: attempts, duration, pending requests, and results.
- WebSockets: active connections, messages, sizes, queue depth, and failures.
- Matchmaking: queue size, wait time, registrations, exits, and matches.
- Lobbies and matches: state, players, duration, finalization, and failures.
- Database: command count, duration, errors, and pool health.
- Background jobs: runs, duration, failures, and last success.
- External services: calls, duration, failures, and publication backlog.
- Security: rate limits, token use, anti-cheat actions, and moderation actions.

Metric labels use small fixed sets such as operation, outcome, playlist, or lobby type. They do not contain player, lobby, or match IDs.

## Database data

Connection strings and database users are removed from traces. SQL text is also removed unless `IncludeDatabaseStatements` is enabled. Do not enable it until the stored data has been reviewed.

Slow-query logs contain only the operation, duration, and result. They never contain SQL or parameters.

EF Core command logging (`Microsoft.EntityFrameworkCore.Database.Command`) is disabled in code so failed-command text cannot reach any log sink.

## Identifiers in logs and traces

Metrics never contain player, lobby, or match IDs. Logs and traces do include user, lobby, and match IDs (pseudonymous) so incidents can be traced. Display names appear only in Debug logs. Treat the collector and Sentry as systems that hold player identifiers.

Per-message WebSocket failures are logged at Error at most once per user per minute; the rest are Debug. Metrics and spans still record every failure.

## Alloy example

This receives all three OTLP signals and prints them for testing:

```alloy
otelcol.receiver.otlp "genonline" {
  grpc {
    endpoint = "0.0.0.0:4317"
  }

  output {
    metrics = [otelcol.processor.batch.genonline.input]
    logs    = [otelcol.processor.batch.genonline.input]
    traces  = [otelcol.processor.batch.genonline.input]
  }
}

otelcol.processor.batch "genonline" {
  output {
    metrics = [otelcol.exporter.debug.genonline.input]
    logs    = [otelcol.exporter.debug.genonline.input]
    traces  = [otelcol.exporter.debug.genonline.input]
  }
}

otelcol.exporter.debug "genonline" {}
```

Replace the debug exporter with the real Prometheus, Loki, and Tempo outputs.

## Useful alerts

- HTTP errors and slow requests.
- Database errors, slow commands, and pool pressure.
- Failed or stale background jobs.
- Publication failures and growing backlog.
- Long matchmaking waits and setup failures.
- WebSocket failures and growing send queues.
- New Sentry error groups.
