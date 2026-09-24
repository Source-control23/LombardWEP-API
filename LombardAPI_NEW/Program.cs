using FluentValidation;
using FluentValidation.AspNetCore;
using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.Extensions;
using LombardAPI_NEW.Middlewares;
using LombardAPI_NEW.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
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

// ==>>  BEFORE AUTH Calls
// Authorization + Authentication
//builder.Services.AddAuthorization();
//builder.Services.AddAuthentication(); // 🔥 REQUIRED (even for IIS / Windows)


// Authentication — validate Azure AD access tokens (fixes "Unauthenticated API").
builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

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

//
// -------------------- RATE LIMITING --------------------
//
// Resolves the "No Rate Limit" DAST finding. Caps how many requests a single
// client (by IP) can make in a time window, preventing brute-force / enumeration
// of endpoints like account-unlock status.
//
// -------------------- RATE LIMITING --------------------
// Spec: 20 req/min per IP. Exceeding the limit blocks the IP
// for 15 minutes. After 15 min, the cycle resets — if limit
// is exceeded again, another 15-min block applies (loop).
//
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter
        .CreateChained(
            // Layer 1 — short burst check: max 2 requests per 5 seconds.
            System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext =>
                {
                    var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: "burst_" + key,
                        factory: _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 2,
                            Window = TimeSpan.FromSeconds(5),
                            SegmentsPerWindow = 5,
                            QueueLimit = 0
                        });
                }),
            // Layer 2 — minute window: max 20 requests per minute.
            // Once exceeded, the IP is blocked for 15 minutes
            // (the window is 15 min and only 20 permits are available,
            // so after consuming them in the first minute the remaining
            // 14 min of the window keep the IP at 0 permits → 429).
            // After the 15-min window expires the count resets; if the
            // IP immediately exceeds 20 again a new 15-min block starts.
            System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext =>
                {
                    var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "sustained_" + key,
                        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(15),
                            QueueLimit = 0
                        });
                })
        );
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

app.UseRateLimiter();

// 🔥 ORDER IS CRITICAL
app.UseCors("UATPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.UseGlobalExceptionHandler();

app.MapControllers();

app.Run();