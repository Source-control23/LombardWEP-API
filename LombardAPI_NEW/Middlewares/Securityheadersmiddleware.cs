namespace LombardAPI_NEW.Middlewares
{
    /// <summary>
    /// Adds standard security response headers to every response.
    /// Resolves the Checkmarx findings:
    ///   - Missing Content Security Policy      -> Content-Security-Policy
    ///   - Missing Framing Policy / Clickjacking -> X-Frame-Options + frame-ancestors
    /// and hardens the API generally (MIME sniffing, referrer leakage).
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

            // Clickjacking / framing: disallow the API from being framed at all.
            headers["X-Frame-Options"] = "DENY";

            // Content Security Policy. This is an API (no HTML UI), so a restrictive
            // default is safe. frame-ancestors 'none' is the modern clickjacking control.
            headers["Content-Security-Policy"] =
                "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

            // Stop browsers MIME-sniffing responses away from the declared content type.
            headers["X-Content-Type-Options"] = "nosniff";

            // Do not leak full URLs (which may carry ids) in the Referer header.
            headers["Referrer-Policy"] = "no-referrer";

            await _next(context);
        }
    }

    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
            => app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}