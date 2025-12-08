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

                var problem = new ProblemDetails
                {
                    Title = "An unexpected error occurred.",
                    Detail = exceptionHandler?.Error.Message,
                    Status = StatusCodes.Status500InternalServerError
                };

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(problem);
            });
        });
    }
}
