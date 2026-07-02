using E6CarSpa.Domain.Entities;
using E6CarSpa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace E6CarSpa.Infrastructure.Data.Seed;

/// <summary>
/// Applies migrations and seeds first-run data: company settings, an admin login,
/// the E6 service catalogue, an inventory of products, and a basic bill-of-materials.
/// Safe to call on every startup — it only inserts what is missing.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db, ILogger? logger = null)
    {
        // Migrations only apply to a relational provider; the in-memory provider used by
        // integration tests has no schema to migrate.
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();

        // Backfill: the SecurityStamp column is added with an empty default on existing rows.
        // Give each a real random stamp so token-revocation works cleanly for pre-upgrade users.
        var stampless = await db.Users.Where(u => u.SecurityStamp == "").ToListAsync();
        if (stampless.Count > 0)
        {
            foreach (var u in stampless) u.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
        }

        if (!await db.CompanySettings.AnyAsync())
        {
            db.CompanySettings.Add(new CompanySettings
            {
                Name = "E6 Car Spa",
                AddressLine1 = "36, Geetha Nagar Main Road",
                AddressLine2 = "Behind Sakthi Mahal, Perundurai Road",
                City = "Erode",
                State = "Tamil Nadu",
                StateCode = "33",
                Pincode = "638011",
                Phone = "+91 9578749449",
                Email = "e6carspaerd@gmail.com",
                InvoicePrefix = "E6/",
                DefaultGstRate = 18m
            });
        }

        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User
            {
                FullName = "Administrator",
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin@123"),
                Role = UserRole.Admin,
                IsActive = true
            });
            logger?.LogWarning(
                "⚠️  SECURITY: Default admin account seeded with password 'admin@123'. " +
                "Change this immediately via Settings → Users.");
        }
        else
        {
            // Warn on every startup if admin still has the default password.
            var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin" && u.IsActive);
            if (adminUser is not null && BCrypt.Net.BCrypt.Verify("admin@123", adminUser.PasswordHash))
                logger?.LogWarning(
                    "⚠️  SECURITY: Admin account is still using the default password 'admin@123'. " +
                    "Change it immediately via Settings → Users.");
        }

        if (!await db.Services.AnyAsync())
            db.Services.AddRange(SeedServices());

        await db.SaveChangesAsync();
    }

    private static List<Service> SeedServices() =>
    [
        new() { Name = "Ceramic Coating",         Category = "Coating",   DefaultPrice = 15000m },
        new() { Name = "Graphene Coating",        Category = "Coating",   DefaultPrice = 20000m },
        new() { Name = "Teflon Machine Polishing",Category = "Polishing", DefaultPrice = 3500m  },
        new() { Name = "Ceramic Water Wash",      Category = "Wash",      DefaultPrice = 800m   },
        new() { Name = "Foam Wash",               Category = "Wash",      DefaultPrice = 400m   },
        new() { Name = "Interior Cleaning",       Category = "Cleaning",  DefaultPrice = 2500m  },
        new() { Name = "Underbody Coating",       Category = "Coating",   DefaultPrice = 4000m  },
        new() { Name = "Rain Repellent",          Category = "Treatment", DefaultPrice = 1200m  },
        new() { Name = "Window Tinting / Sun Film",Category = "Tinting",  DefaultPrice = 6000m  },
        new() { Name = "Tinkering and Painting",  Category = "Bodyshop",  DefaultPrice = 0m     },
        new() { Name = "Headlight Restoration",   Category = "Detailing", DefaultPrice = 1500m  },
        new() { Name = "Engine Bay Cleaning",     Category = "Cleaning",  DefaultPrice = 1000m  },
        new() { Name = "AMC Package (Annual)",    Category = "Package",   DefaultPrice = 12000m },
    ];
}
