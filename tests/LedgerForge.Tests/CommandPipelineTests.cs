using LedgerForge.Application.Abstractions;
using LedgerForge.Application.Commands;
using LedgerForge.Application.Queries;
using LedgerForge.Infrastructure.EventStore;
using LedgerForge.Infrastructure.Messaging;
using LedgerForge.Infrastructure.ReadModel;
using Microsoft.Extensions.Logging.Abstractions;

namespace LedgerForge.Tests;

public sealed class CommandPipelineTests
{
    [Fact]
    public async Task Command_pipeline_projects_a_read_model_after_an_event_is_published()
    {
        var store = new InMemoryEventStore();
        var readModel = new InMemoryReadModel();
        var bus = new InMemoryEventBus(readModel, NullLogger<InMemoryEventBus>.Instance);
        var clock = new FixedClock(DateTimeOffset.Parse("2026-01-01T10:00:00Z"));
        var accountId = Guid.NewGuid();
        var open = new OpenAccountHandler(store, bus, clock);
        var deposit = new DepositMoneyHandler(store, bus, clock);
        var query = new GetAccountHandler(readModel);

        var opened = await open.HandleAsync(new OpenAccountCommand(accountId, "portfolio-user", "BRL", 0, "corr-open"), CancellationToken.None);
        var deposited = await deposit.HandleAsync(new DepositMoneyCommand(accountId, 125.50m, "BRL", "first-funding", opened.Version, "corr-deposit"), CancellationToken.None);
        var view = await query.HandleAsync(new GetAccountQuery(accountId), CancellationToken.None);

        Assert.Equal(1, opened.Version);
        Assert.Equal(2, deposited.Version);
        Assert.NotNull(view);
        Assert.Equal(125.50m, view!.Balance);
        Assert.Equal(2, view.Version);
    }

    [Fact]
    public async Task Stale_expected_version_is_rejected_before_mutating_the_stream()
    {
        var store = new InMemoryEventStore();
        var bus = new InMemoryEventBus(new InMemoryReadModel(), NullLogger<InMemoryEventBus>.Instance);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var accountId = Guid.NewGuid();
        var open = new OpenAccountHandler(store, bus, clock);
        var deposit = new DepositMoneyHandler(store, bus, clock);
        await open.HandleAsync(new OpenAccountCommand(accountId, "portfolio-user", "BRL", 0, "corr-open"), CancellationToken.None);
        await deposit.HandleAsync(new DepositMoneyCommand(accountId, 10m, "BRL", "first", 1, "corr-first"), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<LedgerForge.Domain.Primitives.DomainException>(() =>
            deposit.HandleAsync(new DepositMoneyCommand(accountId, 10m, "BRL", "stale", 1, "corr-stale"), CancellationToken.None));

        Assert.Equal("concurrency.conflict", exception.Code);
        var events = await store.LoadAsync(accountId, CancellationToken.None);
        Assert.Equal(2, events.Count);
    }

    private sealed class FixedClock(DateTimeOffset value) : ISystemClock
    {
        public DateTimeOffset UtcNow => value;
    }
}