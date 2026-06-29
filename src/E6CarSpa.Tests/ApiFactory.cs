using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace E6CarSpa.Tests;

/// <summary>
/// Boots the real API in-process (TestServer) for integration tests, but swaps PostgreSQL for a
/// fresh in-memory database per factory instance. A new factory per test gives each test an isolated
/// database AND an isolated rate-limiter (important for the login throttling test).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "itest-" + Guid.NewGuid();

    static ApiFactory()
    {
        // Provided via environment so Program reads them before the host is built.
        // A dummy connection string keeps Program's startup check happy; the real DbContext is
        // replaced with in-memory below, so it's never used to connect.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ConnectionStrings__Default",
            "Host=localhost;Database=e6test;Username=test;Password=test");
        Environment.SetEnvironmentVariable("Jwt__Key", "e6-integration-tests-signing-key-at-least-32-chars!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "E6CarSpa");
        Environment.SetEnvironmentVariable("Jwt__Audience", "E6CarSpa.Desktop");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove the Npgsql DbContext registration (options + the new options-configuration
            // service + the context itself) and replace it with the in-memory provider.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(AppDbContext) ||
                            d.ServiceType.Name.Contains("DbContextOptions"))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}
