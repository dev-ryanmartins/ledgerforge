using LedgerForge.Application.Abstractions;
using LedgerForge.Application.Contracts;

namespace LedgerForge.Application.Queries;

public sealed class GetAccountHandler(IReadModel readModel) : IQueryHandler<GetAccountQuery, AccountView?>
{
    public Task<AccountView?> HandleAsync(GetAccountQuery query, CancellationToken cancellationToken) =>
        readModel.GetAccountAsync(query.AccountId, cancellationToken);
}

public sealed class ListAccountsHandler(IReadModel readModel) : IQueryHandler<ListAccountsQuery, IReadOnlyList<AccountView>>
{
    public Task<IReadOnlyList<AccountView>> HandleAsync(ListAccountsQuery query, CancellationToken cancellationToken) =>
        readModel.ListAccountsAsync(cancellationToken);
}

public sealed class GetAccountHistoryHandler(IEventStore eventStore) : IQueryHandler<GetAccountHistoryQuery, IReadOnlyList<EventView>>
{
    public async Task<IReadOnlyList<EventView>> HandleAsync(GetAccountHistoryQuery query, CancellationToken cancellationToken)
    {
        var history = await eventStore.LoadAsync(query.AccountId, cancellationToken);
        return history
            .Select(eventEnvelope => new EventView(
                eventEnvelope.EventId,
                eventEnvelope.StreamId,
                eventEnvelope.Version,
                eventEnvelope.EventType,
                eventEnvelope.Data,
                eventEnvelope.OccurredAt,
                eventEnvelope.CorrelationId))
            .ToArray();
    }
}