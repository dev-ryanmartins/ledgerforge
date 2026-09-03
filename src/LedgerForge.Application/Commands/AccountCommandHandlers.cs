using LedgerForge.Application.Abstractions;
using LedgerForge.Domain.Aggregates;
using LedgerForge.Domain.Primitives;

namespace LedgerForge.Application.Commands;

public sealed class OpenAccountHandler(
    IEventStore eventStore,
    IEventBus eventBus,
    ISystemClock clock) : ICommandHandler<OpenAccountCommand, CommandResult>
{
    public async Task<CommandResult> HandleAsync(OpenAccountCommand command, CancellationToken cancellationToken)
    {
        var history = await eventStore.LoadAsync(command.AccountId, cancellationToken);
        var account = BankAccount.Rehydrate(command.AccountId, history);

        if (command.ExpectedVersion != account.Version)
        {
            throw new DomainException(
                "concurrency.conflict",
                $"Expected stream version {command.ExpectedVersion}, actual version is {account.Version}.");
        }

        var opened = BankAccount.Open(command.AccountId, command.OwnerId, command.Currency, clock.UtcNow);
        var version = await SaveAsync(opened, command.CorrelationId, cancellationToken);
        return new CommandResult(opened.Id, version, command.CorrelationId);
    }

    private async Task<long> SaveAsync(BankAccount account, string correlationId, CancellationToken cancellationToken)
    {
        var events = account.DequeueUncommittedEvents();
        var newVersion = account.Version + events.Count;
        await eventStore.AppendAsync(account.Id, account.Version, events, new EventMetadata(correlationId), cancellationToken);
        var committed = await eventStore.LoadAsync(account.Id, cancellationToken);
        await eventBus.PublishAsync(committed.TakeLast(events.Count).ToArray(), cancellationToken);
        return newVersion;
    }
}

public sealed class DepositMoneyHandler(
    IEventStore eventStore,
    IEventBus eventBus,
    ISystemClock clock) : ICommandHandler<DepositMoneyCommand, CommandResult>
{
    public async Task<CommandResult> HandleAsync(DepositMoneyCommand command, CancellationToken cancellationToken)
    {
        var account = await LoadAndValidateVersionAsync(command.AccountId, command.ExpectedVersion, cancellationToken);
        account.Deposit(command.Amount, command.Currency, command.Reference, clock.UtcNow);
        var version = await SaveAsync(account, command.CorrelationId, cancellationToken);
        return new CommandResult(account.Id, version, command.CorrelationId);
    }

    private async Task<BankAccount> LoadAndValidateVersionAsync(Guid id, long expectedVersion, CancellationToken cancellationToken)
    {
        var history = await eventStore.LoadAsync(id, cancellationToken);
        var account = BankAccount.Rehydrate(id, history);
        if (expectedVersion != account.Version)
        {
            throw new DomainException("concurrency.conflict", $"Expected stream version {expectedVersion}, actual version is {account.Version}.");
        }

        return account;
    }

    private async Task<long> SaveAsync(BankAccount account, string correlationId, CancellationToken cancellationToken)
    {
        var events = account.DequeueUncommittedEvents();
        var newVersion = account.Version + events.Count;
        await eventStore.AppendAsync(account.Id, account.Version, events, new EventMetadata(correlationId), cancellationToken);
        var committed = await eventStore.LoadAsync(account.Id, cancellationToken);
        await eventBus.PublishAsync(committed.TakeLast(events.Count).ToArray(), cancellationToken);
        return newVersion;
    }
}

public sealed class WithdrawMoneyHandler(
    IEventStore eventStore,
    IEventBus eventBus,
    ISystemClock clock) : ICommandHandler<WithdrawMoneyCommand, CommandResult>
{
    public async Task<CommandResult> HandleAsync(WithdrawMoneyCommand command, CancellationToken cancellationToken)
    {
        var history = await eventStore.LoadAsync(command.AccountId, cancellationToken);
        var account = BankAccount.Rehydrate(command.AccountId, history);
        if (command.ExpectedVersion != account.Version)
        {
            throw new DomainException("concurrency.conflict", $"Expected stream version {command.ExpectedVersion}, actual version is {account.Version}.");
        }

        account.Withdraw(command.Amount, command.Currency, command.Reference, clock.UtcNow);
        var events = account.DequeueUncommittedEvents();
        var newVersion = account.Version + events.Count;
        await eventStore.AppendAsync(account.Id, account.Version, events, new EventMetadata(command.CorrelationId), cancellationToken);
        var committed = await eventStore.LoadAsync(account.Id, cancellationToken);
        await eventBus.PublishAsync(committed.TakeLast(events.Count).ToArray(), cancellationToken);
        return new CommandResult(account.Id, newVersion, command.CorrelationId);
    }
}