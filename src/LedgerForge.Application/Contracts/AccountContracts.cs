namespace LedgerForge.Application.Contracts;

public sealed record OpenAccountRequest(string OwnerId, string Currency, long ExpectedVersion = 0);

public sealed record MoneyMovementRequest(
    decimal Amount,
    string Currency,
    string Reference,
    long ExpectedVersion);

public sealed record CommandAcceptedResponse(
    Guid AccountId,
    long Version,
    string Status,
    string CorrelationId);

public sealed record AccountView(
    Guid AccountId,
    string OwnerId,
    string Currency,
    decimal Balance,
    bool IsOpen,
    long Version,
    DateTimeOffset LastUpdatedAt);

public sealed record EventView(
    Guid EventId,
    Guid AccountId,
    long Version,
    string Type,
    object Data,
    DateTimeOffset OccurredAt,
    string CorrelationId);