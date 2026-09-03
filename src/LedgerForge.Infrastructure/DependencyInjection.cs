using LedgerForge.Application.Abstractions;
using LedgerForge.Application.Commands;
using LedgerForge.Application.Queries;
using LedgerForge.Infrastructure.Clock;
using LedgerForge.Infrastructure.EventStore;
using LedgerForge.Infrastructure.Messaging;
using LedgerForge.Infrastructure.ReadModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LedgerForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLedgerForgeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var persistence = configuration["Persistence:Provider"] ?? "InMemory";
        var messaging = configuration["Messaging:Provider"] ?? "InMemory";
        var connectionString = configuration.GetConnectionString("LedgerForge");

        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddScoped<OpenAccountHandler>();
        services.AddScoped<DepositMoneyHandler>();
        services.AddScoped<WithdrawMoneyHandler>();
        services.AddScoped<GetAccountHandler>();
        services.AddScoped<ListAccountsHandler>();
        services.AddScoped<GetAccountHistoryHandler>();

        if (string.Equals(persistence, "Postgres", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IEventStore>(_ => new PostgresEventStore(connectionString));
            services.AddSingleton<IReadModel>(_ => new PostgresReadModel(connectionString));
        }
        else
        {
            services.AddSingleton<IEventStore, InMemoryEventStore>();
            services.AddSingleton<IReadModel, InMemoryReadModel>();
        }

        if (string.Equals(messaging, "RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEventBus>(provider =>
                new RabbitMqEventBus(
                    configuration["RabbitMq:Host"] ?? "localhost",
                    configuration["RabbitMq:Exchange"] ?? "ledgerforge.events",
                    provider.GetRequiredService<ILogger<RabbitMqEventBus>>()));
        }
        else
        {
            services.AddSingleton<IEventBus, InMemoryEventBus>();
        }

        return services;
    }
}