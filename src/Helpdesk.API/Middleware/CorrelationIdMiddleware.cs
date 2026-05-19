using Serilog.Context;

namespace Helpdesk.API.Middleware;

public static class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var raw = context.Request.Headers[HeaderName].FirstOrDefault();
            var correlationId = (!string.IsNullOrWhiteSpace(raw) && raw.Length <= 64)
                ? raw
                : Guid.NewGuid().ToString();

            context.Items["CorrelationId"] = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
                await next();
        });
}
