namespace LombardAPI_NEW.Middlewares
{
    /// <summary>
    /// Adds standard security response headers to every response.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            headers["X-Frame-Options"] = "DENY";
            headers["Content-Security-Policy"] =
                "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            // Restrict powerful browser features this API never needs.
            headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";

            // Sensitive API data must not be cached by browsers or proxies.
            headers["Cache-Control"] = "no-store";
            headers["Pragma"] = "no-cache";

            // Additional headers Burp checks for (cross-domain / cross-origin isolation).
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";

            // Explicitly appease the legacy scanner checks
            headers["X-XSS-Protection"] = "1; mode=block";

            // Force strip information disclosure elements from the response
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            await _next(context);
        }
    }

    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
            => app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}