using LedgerForge.Domain.Aggregates;
using LedgerForge.Domain.Events;
using LedgerForge.Domain.Primitives;

namespace LedgerForge.Tests;

public sealed class BankAccountTests
{
    [Fact]
    public void Opening_an_account_emits_a_single_opened_event()
    {
        var accountId = Guid.NewGuid();
        var account = BankAccount.Open(accountId, "portfolio-user", "brl", DateTimeOffset.Parse("2026-01-01T10:00:00Z"));

        var events = account.DequeueUncommittedEvents();

        Assert.Equal(accountId, account.Id);
        Assert.Equal("BRL", account.Currency);
        Assert.True(account.IsOpen);
        Assert.Single(events);
        Assert.IsType<AccountOpened>(events[0]);
    }

    [Fact]
    public void Withdrawal_cannot_exceed_the_current_balance()
    {
        var account = BankAccount.Open(Guid.NewGuid(), "portfolio-user", "BRL", DateTimeOffset.UtcNow);
        account.DequeueUncommittedEvents();
        account.Deposit(100m, "BRL", "initial-funding", DateTimeOffset.UtcNow);
        account.DequeueUncommittedEvents();

        var exception = Assert.Throws<DomainException>(() =>
            account.Withdraw(100.01m, "BRL", "too-large", DateTimeOffset.UtcNow));

        Assert.Equal("account.insufficient_funds", exception.Code);
    }

    [Fact]
    public void Amounts_with_more_than_two_decimal_places_are_rejected()
    {
        var account = BankAccount.Open(Guid.NewGuid(), "portfolio-user", "BRL", DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainException>(() =>
            account.Deposit(10.123m, "BRL", "precision-check", DateTimeOffset.UtcNow));

        Assert.Equal("deposit.amount_invalid", exception.Code);
    }

    [Fact]
    public void Rehydration_replays_history_without_creating_new_events()
    {
        var accountId = Guid.NewGuid();
        var openedAt = DateTimeOffset.Parse("2026-01-01T10:00:00Z");
        var history = new[]
        {
            new EventEnvelope(Guid.NewGuid(), accountId, 1, nameof(AccountOpened), new AccountOpened(accountId, "portfolio-user", "BRL", openedAt), openedAt, "test"),
            new EventEnvelope(Guid.NewGuid(), accountId, 2, nameof(MoneyDeposited), new MoneyDeposited(accountId, 250m, "BRL", "seed", openedAt.AddMinutes(1)), openedAt.AddMinutes(1), "test")
        };

        var account = BankAccount.Rehydrate(accountId, history);

        Assert.Equal(250m, account.Balance);
        Assert.Equal(2, account.Version);
        Assert.Empty(account.UncommittedEvents);
    }
}