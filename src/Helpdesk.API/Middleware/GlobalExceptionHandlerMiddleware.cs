namespace Helpdesk.API.Middleware;

public sealed class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items["CorrelationId"] as string
                ?? context.TraceIdentifier;

            logger.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    error = new { code = "internal_error", message = "An unexpected error occurred." },
                    correlationId,
                    timestamp = DateTime.UtcNow
                });
            }
        }
    }
}
