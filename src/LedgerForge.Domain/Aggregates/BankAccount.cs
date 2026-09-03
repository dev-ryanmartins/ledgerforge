using LedgerForge.Domain.Events;
using LedgerForge.Domain.Primitives;

namespace LedgerForge.Domain.Aggregates;

public sealed class BankAccount
{
    private readonly List<IDomainEvent> _uncommittedEvents = [];

    private BankAccount(Guid id)
    {
        Id = id;
    }

    private BankAccount()
    {
    }

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public decimal Balance { get; private set; }
    public bool IsOpen { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyCollection<IDomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    public static BankAccount Open(Guid id, string ownerId, string currency, DateTimeOffset openedAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("account.id_required", "Account id is required.");
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new DomainException("account.owner_required", "Owner id is required.");
        }

        var normalizedCurrency = NormalizeCurrency(currency);
        var account = new BankAccount(id);
        account.Raise(new AccountOpened(id, ownerId.Trim(), normalizedCurrency, openedAt));
        return account;
    }

    public void Deposit(decimal amount, string currency, string reference, DateTimeOffset depositedAt)
    {
        EnsureOpen();
        EnsureCurrency(currency);
        EnsurePositive(amount, "deposit.amount_invalid");
        EnsureReference(reference);
        Raise(new MoneyDeposited(Id, amount, Currency, reference.Trim(), depositedAt));
    }

    public void Withdraw(decimal amount, string currency, string reference, DateTimeOffset withdrawnAt)
    {
        EnsureOpen();
        EnsureCurrency(currency);
        EnsurePositive(amount, "withdrawal.amount_invalid");
        EnsureReference(reference);

        if (amount > Balance)
        {
            throw new DomainException(
                "account.insufficient_funds",
                $"Withdrawal of {amount:0.00} exceeds available balance of {Balance:0.00}.");
        }

        Raise(new MoneyWithdrawn(Id, amount, Currency, reference.Trim(), withdrawnAt));
    }

    public static BankAccount Rehydrate(Guid id, IEnumerable<EventEnvelope> history)
    {
        var account = new BankAccount(id);
        foreach (var envelope in history.OrderBy(item => item.Version))
        {
            account.Apply(envelope.Data);
            account.Version = envelope.Version;
        }

        return account;
    }

    public IReadOnlyList<IDomainEvent> DequeueUncommittedEvents()
    {
        var events = _uncommittedEvents.ToArray();
        _uncommittedEvents.Clear();
        return events;
    }

    private void Raise(IDomainEvent @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    private void Apply(IDomainEvent @event)
    {
        switch (@event)
        {
            case AccountOpened opened:
                if (IsOpen)
                {
                    throw new DomainException("account.already_open", "Account is already open.");
                }

                OwnerId = opened.OwnerId;
                Currency = opened.Currency;
                Balance = 0m;
                IsOpen = true;
                break;
            case MoneyDeposited deposited:
                Balance += deposited.Amount;
                break;
            case MoneyWithdrawn withdrawn:
                Balance -= withdrawn.Amount;
                break;
            default:
                throw new DomainException("event.unknown", $"Unsupported event '{@event.GetType().Name}'.");
        }
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new DomainException("account.not_open", "Account must be open before it can transact.");
        }
    }

    private void EnsureCurrency(string currency)
    {
        if (!string.Equals(Currency, NormalizeCurrency(currency), StringComparison.Ordinal))
        {
            throw new DomainException("account.currency_mismatch", $"Account currency is {Currency}.");
        }
    }

    private static void EnsurePositive(decimal amount, string code)
    {
        if (amount <= 0m || decimal.Round(amount, 2) != amount)
        {
            throw new DomainException(code, "Amount must be positive and have at most two decimal places.");
        }
    }

    private static void EnsureReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 120)
        {
            throw new DomainException("transaction.reference_invalid", "A reference between 1 and 120 characters is required.");
        }
    }

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new DomainException("account.currency_invalid", "Currency must be an ISO 4217 code.");
        }

        return currency.Trim().ToUpperInvariant();
    }
}