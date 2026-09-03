namespace LedgerForge.Application.Abstractions;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}