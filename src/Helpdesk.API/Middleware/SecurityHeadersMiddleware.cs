namespace Helpdesk.API.Middleware;

public static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var isDocPath = path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase);

            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            context.Response.Headers["Cache-Control"] = "no-store";

            if (!isDocPath)
                context.Response.Headers["Content-Security-Policy"] = "default-src 'none'";

            if (context.Request.IsHttps)
                context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            await next();
        });
    }
}
