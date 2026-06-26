using System.Net;
using System.Text.Json;

namespace Universal.Transfers.Api.Common.Middlewares;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — no response needed
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                error = "An internal server error occurred.",
                requestId = context.TraceIdentifier,
            };

            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);
        }
    }
}
