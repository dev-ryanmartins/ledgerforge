using LedgerForge.Application.Abstractions;

namespace LedgerForge.Infrastructure.Clock;

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}