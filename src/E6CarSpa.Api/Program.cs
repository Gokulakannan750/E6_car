using System.Security.Claims;
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
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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

// Fail fast if the JWT signing key is too weak.
if (jwtOptions.Key.Length < 32 || jwtOptions.Key.Contains("CHANGE_ME") || jwtOptions.Key.Contains("REPLACE_WITH"))
{
    throw new InvalidOperationException(
        "Jwt:Key must be a strong secret of at least 32 characters. " +
        "Set it in appsettings.json or via the E6_JWT_KEY environment variable.");
}

// ----- Database (PostgreSQL) -----
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(rawConn);
    // PendingModelChangesWarning fires when the compiled model differs from the last migration's
    // snapshot — useful in dev, but fatal in production after code-only changes that don't need
    // a new migration (e.g. adding a new entity is handled by our existing migrations).
    opt.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// ----- Auth -----
// The one endpoint an account with MustChangePassword set is still allowed to call.
const string ChangeOwnPasswordPath = "/api/auth/users/me/password";
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
        // Per-request revocation check: a signed, unexpired token is still rejected if the user
        // was deactivated or their security stamp was rotated (password/role change). This is what
        // makes "force logout" / instant deactivation possible without waiting for token expiry.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal is null ||
                    !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                {
                    context.Fail("Invalid token subject.");
                    return;
                }

                var dbCtx = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = await dbCtx.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                var stamp = principal.FindFirst("sstamp")?.Value;
                if (user is null || !user.IsActive || user.SecurityStamp != stamp)
                {
                    context.Fail("Token has been revoked.");
                    return;
                }

                // An account on a machine-generated password (first-run admin, or one rotated off a
                // known default) is allowed exactly one action: setting its own password. Enforced
                // here so no endpoint can forget the check.
                if (user.MustChangePassword &&
                    !context.HttpContext.Request.Path.StartsWithSegments(ChangeOwnPasswordPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("Password change required before this account can be used.");
                }
            }
        };
    });
// Secure-by-default: EVERY endpoint requires an authenticated user. Only /api/auth/login opts
// out with [AllowAnonymous]. The counter previously ran anonymously over the LAN; that surface
// was closed when the API became internet-reachable, so both clients now sign in first.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ----- Reverse-proxy awareness -----
// In production the API sits behind a TLS-terminating reverse proxy (Caddy) on the same host.
// Honour X-Forwarded-For / X-Forwarded-Proto so the app sees the REAL client IP (not the
// proxy's loopback address) — otherwise every client would share one rate-limit bucket — and
// knows the original request was HTTPS. Only loopback proxies are trusted by default.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// ----- Rate limiting -----
// Two layers, both partitioned by the real client IP:
//   • GlobalLimiter — every endpoint, generous cap, stops scraping / crude DoS / brute-force.
//   • "login" policy — much stricter, stacked on top for the auth endpoint specifically.
static string ClientIp(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
builder.Services.AddRateLimiter(options =>
{
    // Applies to ALL requests. Login is additionally constrained by the "login" policy below.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromSeconds(60),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        }));

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromSeconds(60),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        }));

    // Return 429 with a Retry-After header instead of the default empty response.
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 429,
            Title = "Too many requests",
            Detail = "Too many requests. Please wait a moment and try again."
        }, token);
    };
});

// ----- Application services -----
builder.Services.AddHttpContextAccessor();          // AuditService reads caller IP + identity
builder.Services.AddScoped<AuditService>();
// Short timeout: the send runs inside the payment request, so a hung provider must not
// hold up the cashier (failures are recorded in NotificationLog, never thrown).
builder.Services.AddHttpClient("whatsapp", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ReportsService>();
builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddScoped<PdfInvoiceService>();

builder.Services.AddCors(options =>
{
    // Restrictive default policy: allow specific origins (e.g., a future web portal)
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://app.e6carspa.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddControllers();

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
// Must run first so the real client IP / scheme is resolved before rate limiting, logging, etc.
app.UseForwardedHeaders();

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
        Detail = isBadOp ? ex?.Message : "An unexpected error occurred. Please try again or contact support.",
    });
}));

// ----- Security headers -----
// Prevent MIME sniffing, clickjacking, and information leakage.
var isProduction = !app.Environment.IsDevelopment();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
    // HSTS: once the API is served over HTTPS (behind Caddy), tell browsers to only ever use
    // TLS for a year. Emitted in Production only; HTTP clients/browsers ignore it over plain
    // HTTP and native app clients don't act on it, so it's safe to always send in Production.
    if (isProduction)
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    // (Server header is suppressed at the Kestrel level via AddServerHeader = false.)
    await next();
});

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so the integration-test WebApplicationFactory<Program> can boot the real app.
public partial class Program;
