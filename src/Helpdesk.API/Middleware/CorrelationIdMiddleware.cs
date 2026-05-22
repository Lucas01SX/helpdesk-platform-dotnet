using System.Text.RegularExpressions;
using Serilog.Context;

namespace Helpdesk.API.Middleware;

public static partial class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$")]
    private static partial Regex SafeCorrelationId();

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var raw = context.Request.Headers[HeaderName].FirstOrDefault();
            var correlationId = (!string.IsNullOrWhiteSpace(raw)
                                 && raw.Length <= MaxLength
                                 && SafeCorrelationId().IsMatch(raw))
                ? raw
                : Guid.NewGuid().ToString();

            context.Items["CorrelationId"] = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
                await next();
        });
}
