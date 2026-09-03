using LedgerForge.Application.Abstractions;
using LedgerForge.Application.Contracts;
using Npgsql;

namespace LedgerForge.Infrastructure.ReadModel;

public sealed class PostgresReadModel(string connectionString) : IReadModel
{
    public async Task<AccountView?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT account_id, owner_id, currency, balance, is_open, version, last_updated_at
            FROM account_projection WHERE account_id = @account_id;
            """,
            connection);
        command.Parameters.AddWithValue("account_id", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadAccount(reader)
            : null;
    }

    public async Task<IReadOnlyList<AccountView>> ListAccountsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT account_id, owner_id, currency, balance, is_open, version, last_updated_at FROM account_projection ORDER BY last_updated_at DESC;",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var accounts = new List<AccountView>();
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(ReadAccount(reader));
        }

        return accounts;
    }

    public async Task ProjectAsync(IReadOnlyList<LedgerForge.Domain.Events.EventEnvelope> events, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var envelope in events)
        {
            switch (envelope.Data)
            {
                case LedgerForge.Domain.Events.AccountOpened opened:
                    await ExecuteAsync(
                        connection,
                        transaction,
                        """
                        INSERT INTO account_projection
                            (account_id, owner_id, currency, balance, is_open, version, last_updated_at)
                        VALUES (@account_id, @owner_id, @currency, 0, true, @version, @last_updated_at)
                        ON CONFLICT (account_id) DO UPDATE SET version = EXCLUDED.version;
                        """,
                        ("account_id", opened.AccountId),
                        ("owner_id", opened.OwnerId),
                        ("currency", opened.Currency),
                        ("version", envelope.Version),
                        ("last_updated_at", opened.OpenedAt));
                    break;
                case LedgerForge.Domain.Events.MoneyDeposited deposited:
                    await ExecuteAsync(connection, transaction,
                        "UPDATE account_projection SET balance = balance + @amount, version = @version, last_updated_at = @at WHERE account_id = @account_id;",
                        ("amount", deposited.Amount), ("version", envelope.Version), ("at", deposited.DepositedAt), ("account_id", deposited.AccountId));
                    break;
                case LedgerForge.Domain.Events.MoneyWithdrawn withdrawn:
                    await ExecuteAsync(connection, transaction,
                        "UPDATE account_projection SET balance = balance - @amount, version = @version, last_updated_at = @at WHERE account_id = @account_id;",
                        ("amount", withdrawn.Amount), ("version", envelope.Version), ("at", withdrawn.WithdrawnAt), ("account_id", withdrawn.AccountId));
                    break;
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static AccountView ReadAccount(NpgsqlDataReader reader) =>
        new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetBoolean(4), reader.GetInt64(5), reader.GetFieldValue<DateTimeOffset>(6));

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        await command.ExecuteNonQueryAsync();
    }
}