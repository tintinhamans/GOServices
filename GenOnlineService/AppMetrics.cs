/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
*/

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Sentry;

namespace GenOnlineService
{
    public static class AppMetrics
    {
        public const string MeterName = "GenOnlineService";
        public const string ActivitySourceName = MeterName;
        public const string MySqlActivitySourceName = "MySqlConnector";
        public const string MySqlMeterName = "MySqlConnector";

        private static readonly Meter s_meter = new(MeterName);
        private static readonly ActivitySource s_activitySource = new(ActivitySourceName);
        private static int s_onlinePlayers;
        private static int s_activeLobbies;
        private static int s_pendingLogins;
        private static int s_webSocketSendQueueDepth;
        private static int s_externalPublicationQueueDepth;
        private static double s_externalPublicationOldestAgeSeconds;
        private static Measurement<int>[] s_webSocketConnectionsByType = [];
        private static Measurement<int>[] s_matchmakingPlayersByPlaylist = [];
        private static Measurement<int>[] s_matchmakingBucketsByPlaylist = [];
        private static Measurement<int>[] s_lobbiesByState = [];
        private static Measurement<int>[] s_lobbyPlayersByState = [];
        private static Measurement<long>[] s_backgroundJobLastSuccessByJob = [];
        private static Measurement<int>[] s_backgroundJobFailuresByJob = [];
        private static readonly Counter<long> s_matchmakingRegistrations =
            s_meter.CreateCounter<long>("genonline.matchmaking.registrations", description: "Matchmaking registration attempts");
        private static readonly Histogram<double> s_matchmakingRegistrationDuration =
            s_meter.CreateHistogram<double>("genonline.matchmaking.registration.duration", "s", "Matchmaking registration duration");
        private static readonly Counter<long> s_moderationActions =
            s_meter.CreateCounter<long>("genonline.moderation.actions", description: "Moderation actions applied");
        private static readonly Counter<long> s_externalPublicationResults =
            s_meter.CreateCounter<long>("genonline.external_publication.operations", description: "External leaderboard publication attempts");
        private static readonly Histogram<double> s_externalPublicationDuration =
            s_meter.CreateHistogram<double>("genonline.external_publication.duration", "s", "External leaderboard publication duration");
        private static readonly Counter<long> s_databaseCommands =
            s_meter.CreateCounter<long>("genonline.database.commands", description: "Database commands completed");
        private static readonly Histogram<double> s_databaseCommandDuration =
            s_meter.CreateHistogram<double>("genonline.database.command.duration", "s", "Database command duration");
        private static readonly Counter<long> s_backgroundJobs =
            s_meter.CreateCounter<long>("genonline.background_job.operations", description: "Background job executions");
        private static readonly Histogram<double> s_backgroundJobDuration =
            s_meter.CreateHistogram<double>("genonline.background_job.duration", "s", "Background job duration");
        private static readonly Counter<long> s_webSocketMessages =
            s_meter.CreateCounter<long>("genonline.websocket.messages", description: "Inbound WebSocket messages");
        private static readonly Histogram<long> s_webSocketMessageSize =
            s_meter.CreateHistogram<long>("genonline.websocket.message.size", "By", "Inbound WebSocket message size");
        private static readonly Counter<long> s_lobbyOperations =
            s_meter.CreateCounter<long>("genonline.lobby.operations", description: "Lobby lifecycle operations");
        private static readonly Counter<long> s_authenticationOperations =
            s_meter.CreateCounter<long>("genonline.authentication.operations", description: "Authentication flow outcomes");
        private static readonly Histogram<double> s_authenticationDuration =
            s_meter.CreateHistogram<double>("genonline.authentication.duration", "s", "Authentication flow duration");
        private static readonly Counter<long> s_webSocketConnections =
            s_meter.CreateCounter<long>("genonline.websocket.connections", description: "WebSocket connection lifecycle outcomes");
        private static readonly Counter<long> s_webSocketOutboundMessages =
            s_meter.CreateCounter<long>("genonline.websocket.outbound.messages", description: "Outbound WebSocket send outcomes");
        private static readonly Histogram<long> s_webSocketOutboundMessageSize =
            s_meter.CreateHistogram<long>("genonline.websocket.outbound.message.size", "By", "Outbound WebSocket message size");
        private static readonly Counter<long> s_chatRateLimitRejections =
            s_meter.CreateCounter<long>("genonline.chat.rate_limit.rejections", description: "Chat messages rejected by rate limiting");
        private static readonly Counter<long> s_matchmakingQueueExits =
            s_meter.CreateCounter<long>("genonline.matchmaking.queue.exits", description: "Reasons players leave matchmaking");
        private static readonly Histogram<double> s_matchmakingWaitDuration =
            s_meter.CreateHistogram<double>("genonline.matchmaking.wait.duration", "s", "Time spent searching for a match");
        private static readonly Counter<long> s_matchmakingMatches =
            s_meter.CreateCounter<long>("genonline.matchmaking.matches", description: "Matchmaking formation and setup outcomes");
        private static readonly Counter<long> s_lobbyStateTransitions =
            s_meter.CreateCounter<long>("genonline.lobby.state_transitions", description: "Lobby state transitions");
        private static readonly Counter<long> s_matchOperations =
            s_meter.CreateCounter<long>("genonline.match.operations", description: "Match lifecycle outcomes");
        private static readonly Histogram<double> s_matchDuration =
            s_meter.CreateHistogram<double>("genonline.match.duration", "s", "Elapsed in-game match duration");
        private static readonly Counter<long> s_matchPlayerEvents =
            s_meter.CreateCounter<long>("genonline.match.player_events", description: "Player events affecting a match outcome");
        private static readonly Histogram<double> s_matchFinalizationDuration =
            s_meter.CreateHistogram<double>("genonline.match.finalization.duration", "s", "Match finalization transaction duration");
        private static readonly Counter<long> s_dependencyOperations =
            s_meter.CreateCounter<long>("genonline.dependency.operations", description: "External dependency operation outcomes");
        private static readonly Histogram<double> s_dependencyDuration =
            s_meter.CreateHistogram<double>("genonline.dependency.duration", "s", "External dependency operation duration");
        private static readonly Counter<long> s_tokenOperations =
            s_meter.CreateCounter<long>("genonline.token.operations", description: "Token issue, rotation, validation, and revocation outcomes");
        private static readonly Counter<long> s_antiCheatActions =
            s_meter.CreateCounter<long>("genonline.anticheat.actions", description: "Anti-cheat review and enforcement outcomes");
        private static readonly Counter<long> s_rateLimitRejections =
            s_meter.CreateCounter<long>("genonline.http.rate_limit.rejections", description: "HTTP requests rejected by rate limiting");
        private static readonly ObservableGauge<int> s_onlinePlayersGauge =
            s_meter.CreateObservableGauge("genonline.players.online", () => Volatile.Read(ref s_onlinePlayers), "{player}", "Players currently online");
        private static readonly ObservableGauge<int> s_activeLobbiesGauge =
            s_meter.CreateObservableGauge("genonline.lobbies.active", () => Volatile.Read(ref s_activeLobbies), "{lobby}", "Lobbies currently active");
        private static readonly ObservableGauge<int> s_pendingLoginsGauge =
            s_meter.CreateObservableGauge("genonline.authentication.pending", () => Volatile.Read(ref s_pendingLogins), "{login}", "Pending login-code flows");
        private static readonly ObservableGauge<int> s_webSocketSendQueueDepthGauge =
            s_meter.CreateObservableGauge("genonline.websocket.send_queue.depth", () => Volatile.Read(ref s_webSocketSendQueueDepth), "{message}", "Messages waiting for WebSocket delivery");
        private static readonly ObservableGauge<int> s_externalPublicationQueueDepthGauge =
            s_meter.CreateObservableGauge("genonline.external_publication.queue.depth", () => Volatile.Read(ref s_externalPublicationQueueDepth), "{item}", "Unpublished external leaderboard items");
        private static readonly ObservableGauge<double> s_externalPublicationOldestAgeGauge =
            s_meter.CreateObservableGauge("genonline.external_publication.queue.oldest.age", () => Volatile.Read(ref s_externalPublicationOldestAgeSeconds), "s", "Age of the oldest unpublished item");
        private static readonly ObservableGauge<int> s_webSocketActiveGauge =
            s_meter.CreateObservableGauge("genonline.websocket.connections.active", () => Volatile.Read(ref s_webSocketConnectionsByType), "{connection}", "Active WebSocket connections by session type");
        private static readonly ObservableGauge<int> s_matchmakingQueuedPlayersGauge =
            s_meter.CreateObservableGauge("genonline.matchmaking.queue.players", () => Volatile.Read(ref s_matchmakingPlayersByPlaylist), "{player}", "Queued matchmaking players by playlist");
        private static readonly ObservableGauge<int> s_matchmakingBucketsGauge =
            s_meter.CreateObservableGauge("genonline.matchmaking.buckets", () => Volatile.Read(ref s_matchmakingBucketsByPlaylist), "{bucket}", "Active matchmaking buckets by playlist");
        private static readonly ObservableGauge<int> s_lobbiesByStateGauge =
            s_meter.CreateObservableGauge("genonline.lobbies.by_state", () => Volatile.Read(ref s_lobbiesByState), "{lobby}", "Lobbies by type and state");
        private static readonly ObservableGauge<int> s_lobbyPlayersByStateGauge =
            s_meter.CreateObservableGauge("genonline.lobby.players", () => Volatile.Read(ref s_lobbyPlayersByState), "{player}", "Human players in lobbies by type and state");
        private static readonly ObservableGauge<long> s_backgroundJobLastSuccessGauge =
            s_meter.CreateObservableGauge("genonline.background_job.last_success.time", () => Volatile.Read(ref s_backgroundJobLastSuccessByJob), "s", "Unix time of the most recent successful execution");
        private static readonly ObservableGauge<int> s_backgroundJobConsecutiveFailuresGauge =
            s_meter.CreateObservableGauge("genonline.background_job.consecutive_failures", () => Volatile.Read(ref s_backgroundJobFailuresByJob), "{failure}", "Consecutive failed executions");

        public static void RecordMatchmakingRegistration(string outcome, TimeSpan? duration = null)
        {
            KeyValuePair<string, object?>[] attributes = [new("outcome", outcome)];
            s_matchmakingRegistrations.Add(1, attributes);
            EmitSentryCounter("genonline.matchmaking.registrations", 1, attributes);
            if (duration is { } elapsed)
            {
                double seconds = elapsed.TotalSeconds;
                s_matchmakingRegistrationDuration.Record(seconds, attributes);
                EmitSentryDistribution("genonline.matchmaking.registration.duration", seconds, attributes);
            }
        }

        public static void RecordModerationAction(EModerationAction action)
        {
            RecordModerationAction(action, "applied");
        }

        public static void RecordModerationAction(EModerationAction action, string outcome)
        {
            KeyValuePair<string, object?>[] attributes =
            [
                new("action", action.ToString().ToLowerInvariant()),
                new("outcome", outcome)
            ];
            s_moderationActions.Add(1, attributes);
            EmitSentryCounter("genonline.moderation.actions", 1, attributes);
        }

        public static void RecordExternalPublicationResult(string outcome, TimeSpan? duration = null)
        {
            KeyValuePair<string, object?>[] attributes = [new("outcome", outcome)];
            s_externalPublicationResults.Add(1, attributes);
            EmitSentryCounter("genonline.external_publication.operations", 1, attributes);
            if (duration is { } elapsed)
            {
                double seconds = elapsed.TotalSeconds;
                s_externalPublicationDuration.Record(seconds, attributes);
                EmitSentryDistribution("genonline.external_publication.duration", seconds, attributes);
            }
        }

        public static void RecordDatabaseCommand(string operation, string outcome, TimeSpan duration)
        {
            KeyValuePair<string, object?>[] attributes = [new("operation", operation), new("outcome", outcome)];
            s_databaseCommands.Add(1, attributes);
            s_databaseCommandDuration.Record(duration.TotalSeconds, attributes);
            EmitSentryCounter("genonline.database.commands", 1, attributes);
            EmitSentryDistribution("genonline.database.command.duration", duration.TotalSeconds, attributes);
        }

        public static void RecordBackgroundJob(string job, string outcome, TimeSpan duration)
        {
            KeyValuePair<string, object?>[] attributes = [new("job", job), new("outcome", outcome)];
            KeyValuePair<string, object?> jobAttribute = new("job", job);
            s_backgroundJobs.Add(1, attributes);
            s_backgroundJobDuration.Record(duration.TotalSeconds, attributes);
            EmitSentryCounter("genonline.background_job.operations", 1, attributes);
            EmitSentryDistribution("genonline.background_job.duration", duration.TotalSeconds, attributes);

            if (String.Equals(outcome, "success", StringComparison.Ordinal))
            {
                long successTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                UpdateMeasurement(ref s_backgroundJobLastSuccessByJob, jobAttribute, successTime);
                UpdateMeasurement(ref s_backgroundJobFailuresByJob, jobAttribute, 0);
                EmitSentryGauge("genonline.background_job.last_success.time", successTime, [jobAttribute], "second");
                EmitSentryGauge("genonline.background_job.consecutive_failures", 0, [jobAttribute]);
            }
            else
            {
                int failures = IncrementMeasurement(ref s_backgroundJobFailuresByJob, jobAttribute);
                EmitSentryGauge("genonline.background_job.consecutive_failures", failures, [jobAttribute]);
            }
        }

        public static void RecordWebSocketMessage(string messageType, string outcome, int sizeBytes)
        {
            KeyValuePair<string, object?>[] attributes = [new("message.type", messageType), new("outcome", outcome)];
            KeyValuePair<string, object?>[] sizeAttributes = [new("message.type", messageType)];
            s_webSocketMessages.Add(1, attributes);
            s_webSocketMessageSize.Record(sizeBytes, sizeAttributes);
            EmitSentryCounter("genonline.websocket.messages", 1, attributes);
            EmitSentryDistribution("genonline.websocket.message.size", sizeBytes, sizeAttributes, "byte");
        }

        public static void RecordLobbyOperation(string operation, string outcome, string lobbyType)
        {
            KeyValuePair<string, object?>[] attributes =
            [
                new("operation", operation),
                new("outcome", outcome),
                new("lobby.type", lobbyType)
            ];
            s_lobbyOperations.Add(1, attributes);
            EmitSentryCounter("genonline.lobby.operations", 1, attributes);
        }

        public static void RecordServiceSnapshot(int activeLobbies, int onlinePlayers)
        {
            Volatile.Write(ref s_activeLobbies, activeLobbies);
            Volatile.Write(ref s_onlinePlayers, onlinePlayers);
            EmitSentryGauge("genonline.lobbies.active", activeLobbies);
            EmitSentryGauge("genonline.players.online", onlinePlayers);
        }

        public static void RecordAuthentication(string flow, string outcome, string source, TimeSpan duration)
        {
            KeyValuePair<string, object?>[] attributes =
            [
                new("flow", flow),
                new("outcome", outcome),
                new("source", source)
            ];
            double seconds = duration.TotalSeconds;
            s_authenticationOperations.Add(1, attributes);
            s_authenticationDuration.Record(seconds, attributes);
            EmitSentryCounter("genonline.authentication.operations", 1, attributes);
            EmitSentryDistribution("genonline.authentication.duration", seconds, attributes);
        }

        public static IDisposable MeasureAuthentication(string flow, string source, Func<string> getOutcome)
        {
            Activity? activity = StartActivity(
                $"authentication.{flow}",
                ActivityKind.Internal,
                new("authentication.flow", flow),
                new("authentication.source", source));
            return new TimedMeasurement(duration =>
            {
                try
                {
                    string outcome = getOutcome();
                    RecordAuthentication(flow, outcome, source, duration);
                    activity?.SetTag("authentication.outcome", outcome);
                }
                finally
                {
                    activity?.Dispose();
                }
            });
        }

        public static void RecordPendingLoginSnapshot(int pendingLogins)
        {
            Volatile.Write(ref s_pendingLogins, pendingLogins);
            EmitSentryGauge("genonline.authentication.pending", pendingLogins);
        }

        public static void RecordWebSocketConnection(string operation, string outcome, EUserSessionType sessionType)
        {
            KeyValuePair<string, object?>[] attributes =
            [
                new("operation", operation),
                new("outcome", outcome),
                new("session.type", sessionType.ToString().ToLowerInvariant())
            ];
            s_webSocketConnections.Add(1, attributes);
            EmitSentryCounter("genonline.websocket.connections", 1, attributes);
        }

        public static void RecordWebSocketConnectionSnapshot(IEnumerable<KeyValuePair<EUserSessionType, int>> connections)
        {
            Measurement<int>[] measurements = connections
                .Select(connection => new Measurement<int>(
                    connection.Value,
                    new KeyValuePair<string, object?>("session.type", connection.Key.ToString().ToLowerInvariant())))
                .ToArray();
            Volatile.Write(ref s_webSocketConnectionsByType, measurements);
            foreach (Measurement<int> measurement in measurements)
            {
                EmitSentryGauge("genonline.websocket.connections.active", measurement.Value, measurement.Tags.ToArray());
            }
        }

        public static void AdjustWebSocketSendQueueDepth(int delta)
        {
            int depth = Interlocked.Add(ref s_webSocketSendQueueDepth, delta);
            if (depth < 0)
            {
                Interlocked.Exchange(ref s_webSocketSendQueueDepth, 0);
                depth = 0;
            }
            EmitSentryGauge("genonline.websocket.send_queue.depth", depth);
        }

        public static void RecordWebSocketOutbound(string outcome, int sizeBytes)
        {
            KeyValuePair<string, object?>[] attributes = [new("outcome", outcome)];
            s_webSocketOutboundMessages.Add(1, attributes);
            s_webSocketOutboundMessageSize.Record(sizeBytes, attributes);
            EmitSentryCounter("genonline.websocket.outbound.messages", 1, attributes);
            EmitSentryDistribution("genonline.websocket.outbound.message.size", sizeBytes, attributes, "byte");
        }

        public static void RecordChatRateLimitRejection(string scope)
        {
            KeyValuePair<string, object?>[] attributes = [new("scope", scope)];
            s_chatRateLimitRejections.Add(1, attributes);
            EmitSentryCounter("genonline.chat.rate_limit.rejections", 1, attributes);
        }

        public static void RecordMatchmakingSnapshot(string playlist, int players, int buckets)
        {
            KeyValuePair<string, object?> attribute = new("playlist", playlist);
            UpdateMeasurement(ref s_matchmakingPlayersByPlaylist, attribute, players);
            UpdateMeasurement(ref s_matchmakingBucketsByPlaylist, attribute, buckets);
            EmitSentryGauge("genonline.matchmaking.queue.players", players, [attribute]);
            EmitSentryGauge("genonline.matchmaking.buckets", buckets, [attribute]);
        }

        public static void RecordMatchmakingQueueExit(string playlist, string outcome, TimeSpan duration)
        {
            KeyValuePair<string, object?>[] attributes = [new("playlist", playlist), new("outcome", outcome)];
            double seconds = Math.Max(0, duration.TotalSeconds);
            s_matchmakingQueueExits.Add(1, attributes);
            s_matchmakingWaitDuration.Record(seconds, attributes);
            EmitSentryCounter("genonline.matchmaking.queue.exits", 1, attributes);
            EmitSentryDistribution("genonline.matchmaking.wait.duration", seconds, attributes);
        }

        public static void RecordMatchmakingMatch(string playlist, string outcome)
        {
            KeyValuePair<string, object?>[] attributes = [new("playlist", playlist), new("outcome", outcome)];
            s_matchmakingMatches.Add(1, attributes);
            EmitSentryCounter("genonline.matchmaking.matches", 1, attributes);
        }

        public static void RecordLobbyStateTransition(string lobbyType, string fromState, string toState)
        {
            KeyValuePair<string, object?>[] attributes =
            [
                new("lobby.type", lobbyType),
                new("state.from", fromState),
                new("state.to", toState)
            ];
            s_lobbyStateTransitions.Add(1, attributes);
            EmitSentryCounter("genonline.lobby.state_transitions", 1, attributes);
        }

        public static void RecordLobbySnapshot(string lobbyType, string state, int lobbies, int players)
        {
            KeyValuePair<string, object?>[] attributes = [new("lobby.type", lobbyType), new("state", state)];
            UpdateMeasurement(ref s_lobbiesByState, attributes, lobbies);
            UpdateMeasurement(ref s_lobbyPlayersByState, attributes, players);
            EmitSentryGauge("genonline.lobbies.by_state", lobbies, attributes);
            EmitSentryGauge("genonline.lobby.players", players, attributes);
        }

        public static void RecordMatchOperation(string operation, string outcome, string lobbyType, TimeSpan? duration = null)
        {
            KeyValuePair<string, object?>[] attributes =
            [
                new("operation", operation),
                new("outcome", outcome),
                new("lobby.type", lobbyType)
            ];
            s_matchOperations.Add(1, attributes);
            EmitSentryCounter("genonline.match.operations", 1, attributes);
            if (duration is { } elapsed)
            {
                double seconds = Math.Max(0, elapsed.TotalSeconds);
                s_matchDuration.Record(seconds, attributes);
                EmitSentryDistribution("genonline.match.duration", seconds, attributes);
            }
        }

        public static void RecordMatchPlayerEvent(string eventName, string lobbyType)
        {
            KeyValuePair<string, object?>[] attributes = [new("event", eventName), new("lobby.type", lobbyType)];
            s_matchPlayerEvents.Add(1, attributes);
            EmitSentryCounter("genonline.match.player_events", 1, attributes);
        }

        public static void RecordMatchFinalization(string outcome, string lobbyType, TimeSpan duration)
        {
            KeyValuePair<string, object?>[] attributes = [new("outcome", outcome), new("lobby.type", lobbyType)];
            double seconds = duration.TotalSeconds;
            s_matchFinalizationDuration.Record(seconds, attributes);
            EmitSentryDistribution("genonline.match.finalization.duration", seconds, attributes);
        }

        public static void RecordDependency(string dependency, string operation, string outcome, TimeSpan duration)
        {
            KeyValuePair<string, object?>[] attributes =
            [
                new("dependency", dependency),
                new("operation", operation),
                new("outcome", outcome)
            ];
            double seconds = duration.TotalSeconds;
            s_dependencyOperations.Add(1, attributes);
            s_dependencyDuration.Record(seconds, attributes);
            EmitSentryCounter("genonline.dependency.operations", 1, attributes);
            EmitSentryDistribution("genonline.dependency.duration", seconds, attributes);
        }

        public static IDisposable MeasureDependency(string dependency, string operation, Func<string> getOutcome)
        {
            Activity? activity = StartActivity(
                $"dependency.{dependency}.{operation}",
                ActivityKind.Client,
                new("dependency.name", dependency),
                new("dependency.operation", operation));
            return new TimedMeasurement(duration =>
            {
                try
                {
                    string outcome = getOutcome();
                    RecordDependency(dependency, operation, outcome, duration);
                    activity?.SetTag("dependency.outcome", outcome);
                    if (!String.Equals(outcome, "success", StringComparison.Ordinal)
                        && !String.Equals(outcome, "disabled", StringComparison.Ordinal))
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, outcome);
                    }
                }
                finally
                {
                    activity?.Dispose();
                }
            });
        }

        public static void RecordExternalPublicationQueueSnapshot(int depth, TimeSpan oldestAge)
        {
            Volatile.Write(ref s_externalPublicationQueueDepth, Math.Max(0, depth));
            Volatile.Write(ref s_externalPublicationOldestAgeSeconds, Math.Max(0, oldestAge.TotalSeconds));
            EmitSentryGauge("genonline.external_publication.queue.depth", depth);
            EmitSentryGauge("genonline.external_publication.queue.oldest.age", oldestAge.TotalSeconds, unit: "second");
        }

        public static void RecordTokenOperation(string operation, string outcome, EUserSessionType? sessionType = null)
        {
            List<KeyValuePair<string, object?>> attributes = [new("operation", operation), new("outcome", outcome)];
            if (sessionType is { } type)
            {
                attributes.Add(new("session.type", type.ToString().ToLowerInvariant()));
            }
            s_tokenOperations.Add(1, attributes.ToArray());
            EmitSentryCounter("genonline.token.operations", 1, attributes);
        }

        public static void RecordAntiCheatAction(string action, string outcome)
        {
            KeyValuePair<string, object?>[] attributes = [new("action", action), new("outcome", outcome)];
            s_antiCheatActions.Add(1, attributes);
            EmitSentryCounter("genonline.anticheat.actions", 1, attributes);
        }

        public static void RecordHttpRateLimitRejection()
        {
            s_rateLimitRejections.Add(1);
            EmitSentryCounter("genonline.http.rate_limit.rejections", 1, []);
        }

        public static Activity? StartActivity(
            string name,
            ActivityKind kind = ActivityKind.Internal,
            params KeyValuePair<string, object?>[] attributes)
        {
            Activity? activity = s_activitySource.StartActivity(name, kind);
            if (activity != null)
            {
                foreach (KeyValuePair<string, object?> attribute in attributes)
                {
                    activity.SetTag(attribute.Key, attribute.Value);
                }
            }
            return activity;
        }

        public static void RecordException(Activity? activity, Exception exception)
        {
            if (activity == null)
            {
                return;
            }

            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.AddEvent(new ActivityEvent(
                "exception",
                tags: new ActivityTagsCollection
                {
                    { "exception.type", exception.GetType().FullName },
                    { "exception.message", exception.Message },
                    { "exception.stacktrace", exception.ToString() }
                }));
        }

        private static readonly object s_measurementLock = new();

        private sealed class TimedMeasurement : IDisposable
        {
            private readonly long _startedAt = Stopwatch.GetTimestamp();
            private Action<TimeSpan>? _record;

            public TimedMeasurement(Action<TimeSpan> record)
            {
                _record = record;
            }

            public void Dispose()
            {
                Action<TimeSpan>? record = Interlocked.Exchange(ref _record, null);
                if (record == null)
                {
                    return;
                }

                try
                {
                    record(Stopwatch.GetElapsedTime(_startedAt));
                }
                catch
                {
                    // Telemetry is fail-open.
                }
            }
        }

        private static void UpdateMeasurement(
            ref Measurement<int>[] measurements,
            KeyValuePair<string, object?> attribute,
            int value)
        {
            UpdateMeasurement(ref measurements, [attribute], value);
        }

        private static void UpdateMeasurement(
            ref Measurement<long>[] measurements,
            KeyValuePair<string, object?> attribute,
            long value)
        {
            lock (s_measurementLock)
            {
                List<Measurement<long>> updated = measurements
                    .Where(measurement => !TagsEqual(measurement.Tags, [attribute]))
                    .ToList();
                updated.Add(new Measurement<long>(value, attribute));
                Volatile.Write(ref measurements, updated.ToArray());
            }
        }

        private static int IncrementMeasurement(
            ref Measurement<int>[] measurements,
            KeyValuePair<string, object?> attribute)
        {
            lock (s_measurementLock)
            {
                int value = measurements
                    .FirstOrDefault(measurement => TagsEqual(measurement.Tags, [attribute]))
                    .Value + 1;
                List<Measurement<int>> updated = measurements
                    .Where(measurement => !TagsEqual(measurement.Tags, [attribute]))
                    .ToList();
                updated.Add(new Measurement<int>(value, attribute));
                Volatile.Write(ref measurements, updated.ToArray());
                return value;
            }
        }

        private static void UpdateMeasurement(
            ref Measurement<int>[] measurements,
            KeyValuePair<string, object?>[] attributes,
            int value)
        {
            lock (s_measurementLock)
            {
                List<Measurement<int>> updated = measurements
                    .Where(measurement => !TagsEqual(measurement.Tags, attributes))
                    .ToList();
                updated.Add(new Measurement<int>(value, attributes));
                Volatile.Write(ref measurements, updated.ToArray());
            }
        }

        private static bool TagsEqual(
            ReadOnlySpan<KeyValuePair<string, object?>> left,
            ReadOnlySpan<KeyValuePair<string, object?>> right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; ++index)
            {
                if (!String.Equals(left[index].Key, right[index].Key, StringComparison.Ordinal)
                    || !Equals(left[index].Value, right[index].Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static KeyValuePair<string, object>[] ToSentryAttributes(
            IEnumerable<KeyValuePair<string, object?>> attributes)
        {
            return attributes
                .Select(attribute => new KeyValuePair<string, object>(attribute.Key, attribute.Value ?? "unknown"))
                .ToArray();
        }

        private static void EmitSentryCounter(
            string name,
            long value,
            IEnumerable<KeyValuePair<string, object?>> attributes)
        {
            if (!SentrySdk.IsEnabled)
            {
                return;
            }

            try
            {
                KeyValuePair<string, object>[] sentryAttributes = ToSentryAttributes(attributes);
                SentrySdk.ConfigureScope(scope =>
                    SentrySdk.Metrics.EmitCounter(name, value, sentryAttributes, scope));
            }
            catch
            {
                // Telemetry is fail-open.
            }
        }

        private static void EmitSentryDistribution(
            string name,
            double value,
            IEnumerable<KeyValuePair<string, object?>> attributes,
            string unit = "second")
        {
            if (!SentrySdk.IsEnabled)
            {
                return;
            }

            try
            {
                KeyValuePair<string, object>[] sentryAttributes = ToSentryAttributes(attributes);
                SentrySdk.ConfigureScope(scope =>
                    SentrySdk.Metrics.EmitDistribution(name, value, unit, sentryAttributes, scope));
            }
            catch
            {
                // Telemetry is fail-open.
            }
        }

        private static void EmitSentryGauge(
            string name,
            int value,
            IEnumerable<KeyValuePair<string, object?>>? attributes = null,
            string unit = "none")
        {
            if (!SentrySdk.IsEnabled)
            {
                return;
            }

            try
            {
                KeyValuePair<string, object>[] sentryAttributes = ToSentryAttributes(attributes ?? []);
                SentrySdk.ConfigureScope(scope =>
                    SentrySdk.Metrics.EmitGauge(name, value, unit, sentryAttributes, scope));
            }
            catch
            {
                // Telemetry is fail-open.
            }
        }

        private static void EmitSentryGauge(
            string name,
            double value,
            IEnumerable<KeyValuePair<string, object?>>? attributes = null,
            string unit = "none")
        {
            if (!SentrySdk.IsEnabled)
            {
                return;
            }

            try
            {
                KeyValuePair<string, object>[] sentryAttributes = ToSentryAttributes(attributes ?? []);
                SentrySdk.ConfigureScope(scope =>
                    SentrySdk.Metrics.EmitGauge(name, value, unit, sentryAttributes, scope));
            }
            catch
            {
                // Telemetry is fail-open.
            }
        }
    }
}
