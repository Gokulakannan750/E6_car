using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;

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
    public async Task Reports_AsWorker_ReturnsForbidden()
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

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
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
}
