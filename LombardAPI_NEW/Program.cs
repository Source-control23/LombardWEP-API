using FluentValidation;
using FluentValidation.AspNetCore;
using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.Extensions;
using LombardAPI_NEW.Middlewares;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions
            .CommandTimeout(60)  // Increased from default 30 to 60 seconds
            .EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null
            )
    )
);

// Controllers
builder.Services.AddControllers();

// Authorization + Authentication
// Named policies (SignedInUser / FeatureAccess) declared explicitly on every action
// (Checkmarx "Missing Function Level Authorization"). Same rules as before.
builder.Services.AddLombardAuthorization();

// Microsoft Entra ID (Azure AD) access tokens sent by the Angular app (MSAL interceptor).
// Tokens are validated on every request; every controller is [Authorize] (DAST "Unauthenticated API")
// and privileged screens additionally require the Feature.Access app role. IDs below are public, not secrets.
var azureAd = builder.Configuration.GetSection("AzureAd");
string? tenantId = azureAd["TenantId"];
string? apiClientId = azureAd["ClientId"];
bool azureAdConfigured = Guid.TryParse(tenantId, out _) && Guid.TryParse(apiClientId, out _);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep raw Entra claim names (upn, preferred_username, name...)
        options.RequireHttpsMetadata = true;

        if (!azureAdConfigured)
        {
            // Secure default: without configuration no token can validate, so [Authorize]
            // endpoints return 401 while the rest of the API keeps working.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = Array.Empty<string>(),
                ValidateAudience = true,
                ValidAudiences = Array.Empty<string>(),
                SignatureValidator = (token, _) => throw new SecurityTokenSignatureKeyNotFoundException("AzureAd is not configured.")
            };
            return;
        }

        string instance = (azureAd["Instance"] ?? "https://login.microsoftonline.com/").TrimEnd('/');
        options.Authority = $"{instance}/{tenantId}/v2.0";

        var audiences = new List<string> { apiClientId!, $"api://{apiClientId}" };
        if (!string.IsNullOrWhiteSpace(azureAd["Audience"])) audiences.Add(azureAd["Audience"]!);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // v1 and v2 token issuers of THIS tenant only.
            ValidIssuers = new[]
            {
                $"https://sts.windows.net/{tenantId}/",
                $"{instance}/{tenantId}/v2.0"
            },
            ValidateAudience = true,
            ValidAudiences = audiences,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = "name"
        };
    });

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Custom services
builder.Services.AddLombardServices(
    builder.Configuration,
    new Serilog.Extensions.Logging.SerilogLoggerFactory(logger).CreateLogger("Startup"));

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
builder.Services.AddSwaggerGen(options =>
{
    // Endpoint documentation comes from the XML doc comments on every action
    // (Checkmarx "Undocumented API"). Swagger itself is still served only in Development.
    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

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
// Resolves the "Improper Rate Limiting" DAST finding for EVERY endpoint (GAL Search,
// Account Unlock, Group Membership, Rights Management, Approval). Per client IP:
//   Layer 1 — burst:     2 requests / 5 seconds (sliding window)
//   Layer 2 — sustained: 20 requests / 15 minutes (fixed window; once used up the IP
//                        gets 429 until the window resets)
// Same values as the DAST-cleared build; optionally tunable via the "RateLimiting"
// section (BurstPermitLimit, BurstWindowSeconds, PermitLimit, WindowMinutes).
//
var rateCfg = builder.Configuration.GetSection("RateLimiting");
int burstPermitLimit = rateCfg.GetValue("BurstPermitLimit", 2);
int burstWindowSeconds = rateCfg.GetValue("BurstWindowSeconds", 5);
int sustainedPermitLimit = rateCfg.GetValue("PermitLimit", 20);
int sustainedWindowMinutes = rateCfg.GetValue("WindowMinutes", 15);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter
        .CreateChained(
            System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext =>
                {
                    var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: "burst_" + key,
                        factory: _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = burstPermitLimit,
                            Window = TimeSpan.FromSeconds(burstWindowSeconds),
                            SegmentsPerWindow = 5,
                            QueueLimit = 0
                        });
                }),
            System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext =>
                {
                    var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "sustained_" + key,
                        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                        {
                            PermitLimit = sustainedPermitLimit,
                            Window = TimeSpan.FromMinutes(sustainedWindowMinutes),
                            QueueLimit = 0
                        });
                })
        );
});

var app = builder.Build();

if (!azureAdConfigured)
{
    app.Logger.LogCritical("AzureAd:TenantId / AzureAd:ClientId missing or invalid in configuration. All [Authorize] endpoints will reject all requests (401).");
}

//
// -------------------- MIDDLEWARE --------------------
//

// Apply strict security headers to everything EXCEPT the Swagger UI,
// which needs to load its own scripts/styles.
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/swagger"),
    branch => branch.UseSecurityHeaders());

// HSTS on the guaranteed execution path (browsers ignore it over plain http/localhost).
app.UseHsts();

// Redirect any HTTP request to HTTPS so the HSTS header is actually honoured.
app.UseHttpsRedirection();

// Swagger only in Development (no API inventory exposed in UAT / Production).
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