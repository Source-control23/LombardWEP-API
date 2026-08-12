using FluentValidation;
using FluentValidation.AspNetCore;
using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.Extensions;
using LombardAPI_NEW.Middlewares;
using LombardAPI_NEW.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;

ServicePointManager.SecurityProtocol =
    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

var builder = WebApplication.CreateBuilder(args);

//
// -------------------- LOGGING --------------------
//
var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);

//
// -------------------- SERVICES --------------------
//

// Database
builder.Services.AddDbContext<DataBaseContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// Controllers
builder.Services.AddControllers();

// Authorization + Authentication
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(); // 🔥 REQUIRED (even for IIS / Windows)

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Custom services
builder.Services.AddLombardServices(builder.Configuration);

// HSTS (Strict-Transport-Security) options — tells browsers to only ever
// contact this API over HTTPS. Resolves the "Missing HSTS Header" finding.
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//
// -------------------- CORS (UAT FIXED) --------------------
//
builder.Services.AddCors(options =>
{
    options.AddPolicy("UATPolicy", policy =>
    {
        policy
            // ✅ Avoid localhost / IP / hostname mismatch in UAT
            .SetIsOriginAllowed(origin => true)
            .AllowAnyHeader()
            .AllowAnyMethod();
        // ❌ DO NOT use AllowCredentials
    });
});

var app = builder.Build();

//
// -------------------- MIDDLEWARE --------------------
//


// Apply strict security headers to everything EXCEPT the Swagger UI,
// which needs to load its own scripts/styles.
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/swagger"),
    // Security headers (CSP, X-Frame-Options, etc.) on every response.
    branch => branch.UseSecurityHeaders());

// HSTS is now called UNCONDITIONALLY so it is present on the guaranteed
// execution path. Browsers ignore the header over plain http/localhost, so
// this is safe in every environment and satisfies the "Missing HSTS" check.
app.UseHsts();

// Redirect any HTTP request to HTTPS so the HSTS header is actually honoured.
app.UseHttpsRedirection();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

// 🔥 ORDER IS CRITICAL
app.UseCors("UATPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.UseGlobalExceptionHandler();

app.MapControllers();

app.Run();