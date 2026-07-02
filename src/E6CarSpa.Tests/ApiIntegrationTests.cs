using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Tests;

/// <summary>
/// HTTP-level integration tests over the real API pipeline (routing, auth, role-gating, rate limiting,
/// security headers) — the things unit tests can't see. Each test gets its own isolated app instance.
/// </summary>
public class ApiIntegrationTests
{
    private static async Task<string> LoginAsync(HttpClient client, string user, string pass)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user, pass));
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // ---------- authentication ----------

    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsTokenAndAdminRole()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin@123"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal(UserRole.Admin, body.User.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ---------- authorization / role-gating ----------

    [Fact]
    public async Task AdminEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/auth/users");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_AsAdmin_ReturnsOk()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        Authorize(client, await LoginAsync(client, "admin", "admin@123"));

        var resp = await client.GetAsync("/api/auth/users");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_AsWorker_ReturnsForbidden()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        // Admin creates a Worker, then we log in as that Worker.
        Authorize(client, await LoginAsync(client, "admin", "admin@123"));
        var create = await client.PostAsJsonAsync("/api/auth/users",
            new CreateUserRequest("Floor Worker", "worker1", "worker@123", UserRole.Worker));
        create.EnsureSuccessStatusCode();

        var workerClient = factory.CreateClient();
        Authorize(workerClient, await LoginAsync(workerClient, "worker1", "worker@123"));

        var resp = await workerClient.GetAsync("/api/auth/users");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Reports_AsWorker_ReturnsOk_AfterRoleRestrictionRemoved()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        Authorize(client, await LoginAsync(client, "admin", "admin@123"));
        var create = await client.PostAsJsonAsync("/api/auth/users",
            new CreateUserRequest("Floor Worker", "worker2", "worker@123", UserRole.Worker));
        create.EnsureSuccessStatusCode();

        var workerClient = factory.CreateClient();
        Authorize(workerClient, await LoginAsync(workerClient, "worker2", "worker@123"));

        var resp = await workerClient.GetAsync("/api/reports/sales?from=2026-01-01&to=2026-12-31");

        // Reports now open to all authenticated users (removed Roles="Admin,Manager" restriction)
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Reports_AsAdmin_ReturnsOk()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        Authorize(client, await LoginAsync(client, "admin", "admin@123"));

        var resp = await client.GetAsync("/api/reports/sales?from=2026-01-01&to=2026-12-31");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Products_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Invoices_WithToken_IsAllowed()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        Authorize(client, await LoginAsync(client, "admin", "admin@123"));

        var resp = await client.GetAsync("/api/invoices");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ---------- hardening ----------

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/services"); // anonymous endpoint

        Assert.True(resp.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Contains("nosniff", nosniff!);
        Assert.True(resp.Headers.TryGetValues("X-Frame-Options", out var frame));
        Assert.Contains("DENY", frame!);
    }

    [Fact]
    public async Task Login_IsRateLimited_After5AttemptsPerMinute()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        HttpStatusCode last = HttpStatusCode.OK;
        for (var i = 0; i < 6; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrong"));
            last = resp.StatusCode;
        }

        // First 5 are 401 (bad creds); the 6th in the window is throttled.
        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    // ---------- account lockout ----------

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_LocksTheAccount()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        // 5 wrong attempts (the per-IP limiter permits exactly 5/window, so all reach the controller).
        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrong"));

        await factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.FirstAsync(u => u.Username == "admin");
            Assert.NotNull(admin.LockoutEndAt);
            Assert.True(admin.LockoutEndAt > DateTime.UtcNow);
        });
    }

    [Fact]
    public async Task Login_WhenLocked_RejectsEvenTheCorrectPassword()
    {
        using var factory = new ApiFactory();

        // Lock the account out-of-band, then attempt a login with the RIGHT password.
        await factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.FirstAsync(u => u.Username == "admin");
            admin.LockoutEndAt = DateTime.UtcNow.AddMinutes(15);
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin@123"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ---------- token revocation (security stamp) ----------

    [Fact]
    public async Task Token_IsRevoked_WhenSecurityStampRotates()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        Authorize(client, await LoginAsync(client, "admin", "admin@123"));

        // Token works now.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/users")).StatusCode);

        // Simulate a password reset / forced logout: rotate the stamp.
        await factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.FirstAsync(u => u.Username == "admin");
            admin.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
        });

        // Same (still-unexpired, still-signed) token is now rejected.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/users")).StatusCode);
    }

    [Fact]
    public async Task Token_IsRevoked_WhenUserDeactivated()
    {
        using var factory = new ApiFactory();
        var admin = factory.CreateClient();
        Authorize(admin, await LoginAsync(admin, "admin", "admin@123"));

        var created = await admin.PostAsJsonAsync("/api/auth/users",
            new CreateUserRequest("Temp Worker", "worker9", "worker@123", UserRole.Worker));
        var worker = (await created.Content.ReadFromJsonAsync<UserDto>())!;

        var workerClient = factory.CreateClient();
        Authorize(workerClient, await LoginAsync(workerClient, "worker9", "worker@123"));
        Assert.Equal(HttpStatusCode.OK, (await workerClient.GetAsync("/api/products")).StatusCode);

        // Admin deactivates the worker.
        var deactivate = await admin.PutAsJsonAsync($"/api/auth/users/{worker.Id}",
            new UpdateUserRequest("Temp Worker", UserRole.Worker, IsActive: false, NewPassword: null));
        deactivate.EnsureSuccessStatusCode();

        // The worker's existing token stops working immediately.
        Assert.Equal(HttpStatusCode.Unauthorized, (await workerClient.GetAsync("/api/products")).StatusCode);
    }

    // ---------- audit log ----------

    [Fact]
    public async Task SuccessfulLogin_IsAudited()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await LoginAsync(client, "admin", "admin@123");

        await factory.WithDbAsync(async db =>
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Login.Success" && a.Username == "admin")));
    }

    [Fact]
    public async Task AuditEndpoint_AsAdmin_Ok_AsWorker_Forbidden()
    {
        using var factory = new ApiFactory();
        var admin = factory.CreateClient();
        Authorize(admin, await LoginAsync(admin, "admin", "admin@123"));
        var create = await admin.PostAsJsonAsync("/api/auth/users",
            new CreateUserRequest("Floor Worker", "worker8", "worker@123", UserRole.Worker));
        create.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/audit")).StatusCode);

        var workerClient = factory.CreateClient();
        Authorize(workerClient, await LoginAsync(workerClient, "worker8", "worker@123"));
        Assert.Equal(HttpStatusCode.Forbidden, (await workerClient.GetAsync("/api/audit")).StatusCode);
    }
}
