using System.Diagnostics;

namespace LedgerForge.Api.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? context.TraceIdentifier;
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        var stopwatch = Stopwatch.StartNew();

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            ["HttpMethod"] = context.Request.Method,
            ["RequestPath"] = context.Request.Path.ToString()
        });

        try
        {
            await next(context);
            logger.LogInformation(
                "HTTP request completed. StatusCode={StatusCode} ElapsedMs={ElapsedMs}",
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "HTTP request failed. StatusCode={StatusCode} ElapsedMs={ElapsedMs}",
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}