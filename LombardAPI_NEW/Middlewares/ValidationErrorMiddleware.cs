using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public static class ValidationErrorMiddleware
{
    public static void UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exceptionHandler = context.Features.Get<IExceptionHandlerFeature>();

                // Log the REAL error server-side (full detail stays in logs, never sent to the client).
                if (exceptionHandler?.Error is not null)
                {
                    var logger = context.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("GlobalExceptionHandler");
                    logger.LogError(exceptionHandler.Error, "Unhandled exception while processing request.");
                }

                // Return a GENERIC message. Do NOT expose exception.Message, stack traces,
                // PowerShell errors, or AD structure (Improper Exception Handling).
                var problem = new ProblemDetails
                {
                    Title = "An unexpected error occurred.",
                    Detail = "The request could not be completed. Please contact support if the problem persists.",
                    Status = StatusCodes.Status500InternalServerError
                };

                var h = context.Response.Headers;
                h["X-Frame-Options"] = "DENY";
                h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
                h["X-Content-Type-Options"] = "nosniff";
                h["Referrer-Policy"] = "no-referrer";
                h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                h["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
                h["Cache-Control"] = "no-store";
                h["Pragma"] = "no-cache";
                h["X-Permitted-Cross-Domain-Policies"] = "none";
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                h["Cross-Origin-Resource-Policy"] = "same-origin";
                h["X-XSS-Protection"] = "1; mode=block";

                h.Remove("Server");
                h.Remove("X-Powered-By");

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(problem);
            });
        });
    }
}