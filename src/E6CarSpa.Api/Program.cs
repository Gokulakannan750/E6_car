using System.Text;
using System.Threading.RateLimiting;
using E6CarSpa.Api.Auth;
using E6CarSpa.Api.Config;
using E6CarSpa.Api.Services;
using E6CarSpa.Infrastructure.Data;
using E6CarSpa.Infrastructure.Data.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Run as a Windows Service when started by the Service Control Manager.
// This also sets the content root to the exe's folder, so appsettings.json is always found.
builder.Host.UseWindowsService(o => o.ServiceName = "E6 Car Spa API");

// ----- Kestrel hardening -----
builder.WebHost.ConfigureKestrel(options =>
{
    // Don't advertise the runtime/version in the Server response header.
    options.AddServerHeader = false;
    // Cap request bodies at 5 MB so a malformed/oversized upload can't exhaust memory.
    // (Logo uploads are separately capped at 2 MB in app logic.)
    options.Limits.MaxRequestBodySize = 5 * 1024 * 1024;
});

// ----- Configuration -----
// Allow overriding secrets via environment variables without editing appsettings.json.
// On the shop PC set:  E6_DB_PASSWORD = <real password>
// The connection string in appsettings.json should leave Password=SET_IN_ENV.
var rawConn = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
var dbPassword = Environment.GetEnvironmentVariable("E6_DB_PASSWORD");
if (!string.IsNullOrEmpty(dbPassword))
    rawConn = rawConn.Replace("SET_IN_ENV", dbPassword);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection(WhatsAppOptions.Section));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();

// Allow overriding the JWT key via environment variable: E6_JWT_KEY
var jwtKeyEnv = Environment.GetEnvironmentVariable("E6_JWT_KEY");
if (!string.IsNullOrEmpty(jwtKeyEnv))
    jwtOptions.Key = jwtKeyEnv;

// Fail fast in production if the JWT signing key is too weak.
if (!builder.Environment.IsDevelopment() &&
    (jwtOptions.Key.Length < 32 || jwtOptions.Key.Contains("CHANGE_ME") || jwtOptions.Key.Contains("REPLACE_WITH")))
{
    throw new InvalidOperationException(
        "Jwt:Key must be a strong secret of at least 32 characters in production. " +
        "Set it in appsettings.json or via the E6_JWT_KEY environment variable.");
}

// ----- Database (PostgreSQL) -----
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(rawConn));

// ----- Auth -----
builder.Services.AddSingleton<JwtTokenService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            // Reject tokens that arrive with more than 5 minutes clock skew.
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });
// Secure-by-default: every endpoint requires an authenticated user UNLESS it opts out with
// [AllowAnonymous] (only the login endpoint does). This closes endpoints that lack an explicit
// [Authorize] — important now that the API is reachable over the internet.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ----- Rate limiting -----
// Protects the login endpoint from brute-force attacks.
// Policy "login": max 5 requests per IP per 60 seconds → HTTP 429 on violation.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromSeconds(60),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // Return 429 with a Retry-After header instead of the default empty response.
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 429,
            Title = "Too many requests",
            Detail = "Too many login attempts. Please wait 60 seconds and try again."
        }, token);
    };
});

// ----- Application services -----
builder.Services.AddHttpClient("whatsapp");
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ReportsService>();
builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddScoped<PdfInvoiceService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ----- Migrate + seed on startup -----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DbInitializer");
    await DbInitializer.SeedAsync(db, seedLogger);
}

// ----- Pipeline -----
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var isBadOp = ex is InvalidOperationException;     // our domain rule violations
    var status = isBadOp ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;

    if (!isBadOp)
        context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalException").LogError(ex, "Unhandled exception");

    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = status,
        Title = isBadOp ? "Operation not allowed" : "Server error",
        // Don't leak internal details to the client for 500s.
        Detail = isBadOp ? ex?.Message : "An unexpected error occurred. Please try again or contact support."
    });
}));

// ----- Security headers -----
// Prevent MIME sniffing, clickjacking, and information leakage.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
    // (Server header is suppressed at the Kestrel level via AddServerHeader = false.)
    await next();
});

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so the integration-test WebApplicationFactory<Program> can boot the real app.
public partial class Program;
