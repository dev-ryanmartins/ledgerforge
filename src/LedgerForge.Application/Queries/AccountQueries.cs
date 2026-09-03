using LedgerForge.Application.Abstractions;
using LedgerForge.Application.Contracts;

namespace LedgerForge.Application.Queries;

public sealed record GetAccountQuery(Guid AccountId) : IQuery<AccountView?>;

public sealed record ListAccountsQuery : IQuery<IReadOnlyList<AccountView>>;

public sealed record GetAccountHistoryQuery(Guid AccountId) : IQuery<IReadOnlyList<EventView>>;