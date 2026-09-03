namespace LedgerForge.Domain.Events;

public interface IDomainEvent
{
    string EventType { get; }
}