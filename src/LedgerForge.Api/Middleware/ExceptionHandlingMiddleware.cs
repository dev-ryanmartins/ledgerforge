using System.Text.Json;
using LedgerForge.Domain.Primitives;

namespace LedgerForge.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException exception)
        {
            var statusCode = exception.Code == "concurrency.conflict" ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity;
            await WriteProblemAsync(context, statusCode, exception.Code, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception at API boundary.");
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "server.unhandled", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string code, string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = $"https://ledgerforge.dev/problems/{code}",
            title = "Request could not be completed",
            status = statusCode,
            detail,
            code,
            traceId = context.TraceIdentifier
        }));
    }
}