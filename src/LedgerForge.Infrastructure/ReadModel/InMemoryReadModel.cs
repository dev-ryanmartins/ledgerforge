using LedgerForge.Application.Abstractions;
using LedgerForge.Application.Contracts;
using LedgerForge.Domain.Events;

namespace LedgerForge.Infrastructure.ReadModel;

public sealed class InMemoryReadModel : IReadModel
{
    private readonly Dictionary<Guid, AccountView> _accounts = [];
    private readonly object _gate = new();

    public Task<AccountView?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _accounts.TryGetValue(accountId, out var account);
            return Task.FromResult(account);
        }
    }

    public Task<IReadOnlyList<AccountView>> ListAccountsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<AccountView>>(
                _accounts.Values.OrderByDescending(account => account.LastUpdatedAt).ToArray());
        }
    }

    public Task ProjectAsync(IReadOnlyList<EventEnvelope> events, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            foreach (var envelope in events)
            {
                _accounts.TryGetValue(envelope.StreamId, out var current);
                var next = envelope.Data switch
                {
                    AccountOpened opened => new AccountView(
                        opened.AccountId,
                        opened.OwnerId,
                        opened.Currency,
                        0m,
                        true,
                        envelope.Version,
                        opened.OpenedAt),
                    MoneyDeposited deposited when current is not null => current with
                    {
                        Balance = current.Balance + deposited.Amount,
                        Version = envelope.Version,
                        LastUpdatedAt = deposited.DepositedAt
                    },
                    MoneyWithdrawn withdrawn when current is not null => current with
                    {
                        Balance = current.Balance - withdrawn.Amount,
                        Version = envelope.Version,
                        LastUpdatedAt = withdrawn.WithdrawnAt
                    },
                    _ => current
                };

                if (next is not null)
                {
                    _accounts[envelope.StreamId] = next;
                }
            }
        }

        return Task.CompletedTask;
    }
}