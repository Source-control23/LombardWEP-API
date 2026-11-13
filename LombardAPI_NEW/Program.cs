using FluentValidation;
using FluentValidation.AspNetCore;
using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.Extensions;
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
builder.Services.AddAuthentication(); // 🔥 REQUIRED (even for IIS / Windows)

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