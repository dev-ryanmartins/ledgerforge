using System.Text.Json;
using LedgerForge.Application.Abstractions;
using LedgerForge.Domain.Events;
using LedgerForge.Domain.Primitives;
using Npgsql;

namespace LedgerForge.Infrastructure.EventStore;

public sealed class PostgresEventStore(string connectionString) : IEventStore
{
    public async Task<IReadOnlyList<EventEnvelope>> LoadAsync(Guid streamId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT event_id, stream_id, version, event_type, payload, occurred_at, correlation_id, causation_id
            FROM ledger_events
            WHERE stream_id = @stream_id
            ORDER BY version;
            """,
            connection);
        command.Parameters.AddWithValue("stream_id", streamId);

        var result = new List<EventEnvelope>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var eventType = reader.GetString(3);
            using var document = JsonDocument.Parse(reader.GetString(4));
            var payload = document.RootElement.Clone();
            result.Add(new EventEnvelope(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetInt64(2),
                eventType,
                EventSerializer.Deserialize(eventType, payload),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return result;
    }

    public async Task AppendAsync(
        Guid streamId,
        long expectedVersion,
        IReadOnlyList<IDomainEvent> events,
        EventMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext(@stream_id::text));",
            connection,
            transaction))
        {
            lockCommand.Parameters.AddWithValue("stream_id", streamId);
            await lockCommand.ExecuteScalarAsync(cancellationToken);
        }

        await using (var versionCommand = new NpgsqlCommand(
            "SELECT COALESCE(MAX(version), 0) FROM ledger_events WHERE stream_id = @stream_id;",
            connection,
            transaction))
        {
            versionCommand.Parameters.AddWithValue("stream_id", streamId);
            var actualVersion = (long)(await versionCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
            if (actualVersion != expectedVersion)
            {
                throw new DomainException(
                    "concurrency.conflict",
                    $"Expected stream version {expectedVersion}, actual version is {actualVersion}.");
            }
        }

        var nextVersion = expectedVersion;
        foreach (var @event in events)
        {
            nextVersion++;
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO ledger_events
                    (event_id, stream_id, version, event_type, payload, occurred_at, correlation_id, causation_id)
                VALUES
                    (@event_id, @stream_id, @version, @event_type, @payload::jsonb, @occurred_at, @correlation_id, @causation_id);
                INSERT INTO ledger_outbox
                    (event_id, stream_id, event_type, payload, occurred_at, correlation_id)
                VALUES
                    (@event_id, @stream_id, @event_type, @payload::jsonb, @occurred_at, @correlation_id);
                """,
                connection,
                transaction);
            var payload = JsonSerializer.Serialize(@event, @event.GetType(), EventSerializer.Options);
            insert.Parameters.AddWithValue("event_id", Guid.NewGuid());
            insert.Parameters.AddWithValue("stream_id", streamId);
            insert.Parameters.AddWithValue("version", nextVersion);
            insert.Parameters.AddWithValue("event_type", @event.EventType);
            insert.Parameters.AddWithValue("payload", payload);
            insert.Parameters.AddWithValue("occurred_at", DateTimeOffset.UtcNow);
            insert.Parameters.AddWithValue("correlation_id", metadata.CorrelationId);
            insert.Parameters.AddWithValue("causation_id", (object?)metadata.CausationId ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}

internal static class EventSerializer
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IDomainEvent Deserialize(string eventType, JsonElement payload) =>
        eventType switch
        {
            nameof(AccountOpened) => payload.Deserialize<AccountOpened>(Options)
                ?? throw new InvalidOperationException("Invalid AccountOpened payload."),
            nameof(MoneyDeposited) => payload.Deserialize<MoneyDeposited>(Options)
                ?? throw new InvalidOperationException("Invalid MoneyDeposited payload."),
            nameof(MoneyWithdrawn) => payload.Deserialize<MoneyWithdrawn>(Options)
                ?? throw new InvalidOperationException("Invalid MoneyWithdrawn payload."),
            _ => throw new InvalidOperationException($"Unknown event type '{eventType}'.")
        };
}