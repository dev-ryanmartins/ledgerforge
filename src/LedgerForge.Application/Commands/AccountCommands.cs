using LedgerForge.Application.Abstractions;

namespace LedgerForge.Application.Commands;

public sealed record OpenAccountCommand(
    Guid AccountId,
    string OwnerId,
    string Currency,
    long ExpectedVersion,
    string CorrelationId) : ICommand<CommandResult>;

public sealed record DepositMoneyCommand(
    Guid AccountId,
    decimal Amount,
    string Currency,
    string Reference,
    long ExpectedVersion,
    string CorrelationId) : ICommand<CommandResult>;

public sealed record WithdrawMoneyCommand(
    Guid AccountId,
    decimal Amount,
    string Currency,
    string Reference,
    long ExpectedVersion,
    string CorrelationId) : ICommand<CommandResult>;

public sealed record CommandResult(Guid AccountId, long Version, string CorrelationId);