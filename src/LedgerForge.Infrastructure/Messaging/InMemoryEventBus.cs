using LedgerForge.Application.Abstractions;
using LedgerForge.Domain.Events;
using Microsoft.Extensions.Logging;

namespace LedgerForge.Infrastructure.Messaging;

public sealed class InMemoryEventBus(IReadModel readModel, ILogger<InMemoryEventBus> logger) : IEventBus
{
    public async Task PublishAsync(IReadOnlyList<EventEnvelope> events, CancellationToken cancellationToken)
    {
        foreach (var envelope in events)
        {
            await Task.Yield();
            logger.LogInformation(
                "Domain event published to in-memory transport. EventType={EventType} StreamId={StreamId} Version={Version}",
                envelope.EventType,
                envelope.StreamId,
                envelope.Version);
        }

        await readModel.ProjectAsync(events, cancellationToken);
    }
}