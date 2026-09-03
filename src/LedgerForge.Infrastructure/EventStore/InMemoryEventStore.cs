using LedgerForge.Application.Abstractions;
using LedgerForge.Domain.Events;
using LedgerForge.Domain.Primitives;

namespace LedgerForge.Infrastructure.EventStore;

public sealed class InMemoryEventStore : IEventStore
{
    private readonly Dictionary<Guid, List<EventEnvelope>> _streams = [];
    private readonly object _gate = new();

    public Task<IReadOnlyList<EventEnvelope>> LoadAsync(Guid streamId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var events = _streams.TryGetValue(streamId, out var stream)
                ? stream.ToArray()
                : [];
            return Task.FromResult<IReadOnlyList<EventEnvelope>>(events);
        }
    }

    public Task AppendAsync(
        Guid streamId,
        long expectedVersion,
        IReadOnlyList<IDomainEvent> events,
        EventMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return Task.CompletedTask;
        }

        lock (_gate)
        {
            if (!_streams.TryGetValue(streamId, out var stream))
            {
                stream = [];
                _streams[streamId] = stream;
            }

            var actualVersion = stream.Count;
            if (actualVersion != expectedVersion)
            {
                throw new DomainException(
                    "concurrency.conflict",
                    $"Expected stream version {expectedVersion}, actual version is {actualVersion}.");
            }

            foreach (var @event in events)
            {
                stream.Add(new EventEnvelope(
                    Guid.NewGuid(),
                    streamId,
                    stream.Count + 1,
                    @event.EventType,
                    @event,
                    DateTimeOffset.UtcNow,
                    metadata.CorrelationId,
                    metadata.CausationId));
            }
        }

        return Task.CompletedTask;
    }
}