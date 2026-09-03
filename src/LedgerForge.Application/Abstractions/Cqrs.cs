using LedgerForge.Domain.Events;
using LedgerForge.Application.Contracts;

namespace LedgerForge.Application.Abstractions;

public interface ICommand<TResult>
{
}

public interface IQuery<TResult>
{
}

public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

public interface IEventStore
{
    Task<IReadOnlyList<EventEnvelope>> LoadAsync(Guid streamId, CancellationToken cancellationToken);
    Task AppendAsync(Guid streamId, long expectedVersion, IReadOnlyList<IDomainEvent> events, EventMetadata metadata, CancellationToken cancellationToken);
}

public interface IEventBus
{
    Task PublishAsync(IReadOnlyList<EventEnvelope> events, CancellationToken cancellationToken);
}

public interface IReadModel
{
    Task<AccountView?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccountView>> ListAccountsAsync(CancellationToken cancellationToken);
    Task ProjectAsync(IReadOnlyList<EventEnvelope> events, CancellationToken cancellationToken);
}

public sealed record EventMetadata(string CorrelationId, string? CausationId = null);