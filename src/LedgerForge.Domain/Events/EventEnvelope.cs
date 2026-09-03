namespace LedgerForge.Domain.Events;

public sealed record EventEnvelope(
    Guid EventId,
    Guid StreamId,
    long Version,
    string EventType,
    IDomainEvent Data,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? CausationId = null);