using System.Text.Json;
using LedgerForge.Api.Middleware;
using LedgerForge.Application.Commands;
using LedgerForge.Application.Contracts;
using LedgerForge.Application.Queries;
using LedgerForge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});
builder.Services.AddLedgerForgeInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new
{
    service = "ledgerforge-api",
    status = "healthy",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapPost("/api/accounts/{accountId:guid}", async (
    Guid accountId,
    OpenAccountRequest request,
    OpenAccountHandler handler,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(
        new OpenAccountCommand(accountId, request.OwnerId, request.Currency, request.ExpectedVersion, CorrelationId(context)),
        cancellationToken);
    return Results.Created($"/api/accounts/{result.AccountId}", ToResponse(result));
});

app.MapPost("/api/accounts/{accountId:guid}/deposits", async (
    Guid accountId,
    MoneyMovementRequest request,
    DepositMoneyHandler handler,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(
        new DepositMoneyCommand(accountId, request.Amount, request.Currency, request.Reference, request.ExpectedVersion, CorrelationId(context)),
        cancellationToken);
    return Results.Ok(ToResponse(result));
});

app.MapPost("/api/accounts/{accountId:guid}/withdrawals", async (
    Guid accountId,
    MoneyMovementRequest request,
    WithdrawMoneyHandler handler,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(
        new WithdrawMoneyCommand(accountId, request.Amount, request.Currency, request.Reference, request.ExpectedVersion, CorrelationId(context)),
        cancellationToken);
    return Results.Ok(ToResponse(result));
});

app.MapGet("/api/accounts/{accountId:guid}", async (
    Guid accountId,
    GetAccountHandler handler,
    CancellationToken cancellationToken) =>
{
    var account = await handler.HandleAsync(new GetAccountQuery(accountId), cancellationToken);
    return account is null ? Results.NotFound() : Results.Ok(account);
});

app.MapGet("/api/accounts", async (
    ListAccountsHandler handler,
    CancellationToken cancellationToken) =>
    Results.Ok(await handler.HandleAsync(new ListAccountsQuery(), cancellationToken)));

app.MapGet("/api/accounts/{accountId:guid}/events", async (
    Guid accountId,
    GetAccountHistoryHandler handler,
    CancellationToken cancellationToken) =>
    Results.Ok(await handler.HandleAsync(new GetAccountHistoryQuery(accountId), cancellationToken)));

app.Run();

static string CorrelationId(HttpContext context) =>
    context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? context.TraceIdentifier;

static CommandAcceptedResponse ToResponse(CommandResult result) =>
    new(result.AccountId, result.Version, "accepted", result.CorrelationId);

public partial class Program;