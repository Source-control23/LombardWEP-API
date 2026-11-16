using FluentValidation;
using FluentValidation.AspNetCore;
using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.Extensions;
using LombardAPI_NEW.Models;
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
builder.Services.AddAuthorization();

// Microsoft Entra ID (Azure AD) access tokens sent by the Angular app (MSAL interceptor).
// Tokens are validated on every request; only endpoints marked [Authorize] (the approval
// endpoints) REQUIRE one. Tenant / client IDs are public identifiers, not secrets.
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
builder.Services.AddLombardServices(builder.Configuration);

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

if (!azureAdConfigured)
{
    app.Logger.LogCritical("AzureAd:TenantId / AzureAd:ClientId missing or invalid in configuration. Approval endpoints will reject all requests (401).");
}

//
// -------------------- MIDDLEWARE --------------------
//

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection(); // enable only if HTTPS binding exists

app.UseRouting();

// 🔥 ORDER IS CRITICAL
app.UseCors("UATPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.UseGlobalExceptionHandler();

app.MapControllers();

app.Run();