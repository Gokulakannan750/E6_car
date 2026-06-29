using E6CarSpa.Domain.Entities;
using E6CarSpa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace E6CarSpa.Tests;

/// <summary>
/// Shared in-memory database setup for service tests. Each call gets a fresh, uniquely-named
/// store so tests stay isolated, pre-seeded with the CompanySettings the services expect.
/// </summary>
internal static class TestDb
{
    public static AppDbContext Create(string prefix = "TEST/", decimal defaultGst = 18m)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new AppDbContext(options);
        db.CompanySettings.Add(new CompanySettings
        {
            Name = "Test Company",
            DefaultGstRate = defaultGst,
            InvoicePrefix = prefix
        });
        db.SaveChanges();
        return db;
    }
}
