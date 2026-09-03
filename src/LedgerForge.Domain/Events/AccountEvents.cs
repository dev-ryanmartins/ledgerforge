namespace LedgerForge.Domain.Events;

public sealed record AccountOpened(
    Guid AccountId,
    string OwnerId,
    string Currency,
    DateTimeOffset OpenedAt) : IDomainEvent
{
    public string EventType => nameof(AccountOpened);
}

public sealed record MoneyDeposited(
    Guid AccountId,
    decimal Amount,
    string Currency,
    string Reference,
    DateTimeOffset DepositedAt) : IDomainEvent
{
    public string EventType => nameof(MoneyDeposited);
}

public sealed record MoneyWithdrawn(
    Guid AccountId,
    decimal Amount,
    string Currency,
    string Reference,
    DateTimeOffset WithdrawnAt) : IDomainEvent
{
    public string EventType => nameof(MoneyWithdrawn);
}