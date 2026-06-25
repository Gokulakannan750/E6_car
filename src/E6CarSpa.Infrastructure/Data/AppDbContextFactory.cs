using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace E6CarSpa.Infrastructure.Data;

/// <summary>
/// Used only by the EF Core CLI (migrations/scaffolding) at design time, so the tools
/// don't have to boot the full API. Reads the connection string from the
/// E6_CONNECTION environment variable, falling back to a local default.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("E6_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=e6carspa;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new AppDbContext(options);
    }
}
