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

    // Inventory is tracked as a simple count of units (bottles / pieces).
    // ReorderLevel = number of units below which the item is flagged low-stock.
    private static List<Product> SeedProducts() =>
    [
        new() { Name = "Ceramic Coating Solution", Category = "Coating",    StockQuantity = 0, ReorderLevel = 5,  UnitCost = 800m },
        new() { Name = "Graphene Coating Solution",Category = "Coating",    StockQuantity = 0, ReorderLevel = 5,  UnitCost = 1200m },
        new() { Name = "Teflon Polish",            Category = "Polish",     StockQuantity = 0, ReorderLevel = 5,  UnitCost = 300m },
        new() { Name = "Rubbing Compound",         Category = "Polish",     StockQuantity = 0, ReorderLevel = 5,  UnitCost = 250m },
        new() { Name = "Polishing Compound",       Category = "Polish",     StockQuantity = 0, ReorderLevel = 5,  UnitCost = 250m },
        new() { Name = "Car Shampoo",              Category = "Consumable", StockQuantity = 0, ReorderLevel = 5,  UnitCost = 200m },
        new() { Name = "Carnauba Wax",             Category = "Polish",     StockQuantity = 0, ReorderLevel = 5,  UnitCost = 350m },
        new() { Name = "Rain Repellent Solution",  Category = "Treatment",  StockQuantity = 0, ReorderLevel = 5,  UnitCost = 300m },
        new() { Name = "Underbody Coating Spray",  Category = "Coating",    StockQuantity = 0, ReorderLevel = 5,  UnitCost = 350m },
        new() { Name = "Interior Cleaner",         Category = "Cleaning",   StockQuantity = 0, ReorderLevel = 5,  UnitCost = 250m },
        new() { Name = "Dashboard Polish",         Category = "Cleaning",   StockQuantity = 0, ReorderLevel = 5,  UnitCost = 200m },
        new() { Name = "Glass Cleaner",            Category = "Cleaning",   StockQuantity = 0, ReorderLevel = 5,  UnitCost = 150m },
        new() { Name = "Tyre Polish",              Category = "Cleaning",   StockQuantity = 0, ReorderLevel = 5,  UnitCost = 200m },
        new() { Name = "Sun Control Film",         Category = "Tinting",    StockQuantity = 0, ReorderLevel = 5,  UnitCost = 600m },
        new() { Name = "Microfiber Cloth",         Category = "Consumable", StockQuantity = 0, ReorderLevel = 20, UnitCost = 40m },
        new() { Name = "Polishing Pad",            Category = "Consumable", StockQuantity = 0, ReorderLevel = 10, UnitCost = 120m },
        new() { Name = "Grinding Disc",            Category = "Bodyshop",   StockQuantity = 0, ReorderLevel = 10, UnitCost = 60m },
        new() { Name = "Brush",                    Category = "Consumable", StockQuantity = 0, ReorderLevel = 10, UnitCost = 30m },
        new() { Name = "Masking Tape",             Category = "Consumable", StockQuantity = 0, ReorderLevel = 10, UnitCost = 50m },
        new() { Name = "Body Filler / Putty",      Category = "Bodyshop",   StockQuantity = 0, ReorderLevel = 3,  UnitCost = 350m },
        new() { Name = "Primer",                   Category = "Bodyshop",   StockQuantity = 0, ReorderLevel = 3,  UnitCost = 500m },
        new() { Name = "Automotive Paint",         Category = "Bodyshop",   StockQuantity = 0, ReorderLevel = 3,  UnitCost = 1200m },
        new() { Name = "Thinner",                  Category = "Bodyshop",   StockQuantity = 0, ReorderLevel = 5,  UnitCost = 150m },
        new() { Name = "Sandpaper",                Category = "Consumable", StockQuantity = 0, ReorderLevel = 20, UnitCost = 15m },
    ];

    private static async Task SeedBillOfMaterialsAsync(AppDbContext db)
    {
        var services = await db.Services.ToDictionaryAsync(s => s.Name, s => s.Id);
        var products = await db.Products.ToDictionaryAsync(p => p.Name, p => p.Id);

        void Link(string service, string product, decimal qty)
        {
            if (services.TryGetValue(service, out var sid) && products.TryGetValue(product, out var pid))
                db.ServiceProducts.Add(new ServiceProduct { ServiceId = sid, ProductId = pid, DefaultQuantity = qty });
        }

        // Quantities are whole units consumed per service (editable later in the Catalogue screen).
        Link("Ceramic Coating", "Ceramic Coating Solution", 1);
        Link("Ceramic Coating", "Microfiber Cloth", 4);
        Link("Ceramic Coating", "Polishing Pad", 1);

        Link("Graphene Coating", "Graphene Coating Solution", 1);
        Link("Graphene Coating", "Microfiber Cloth", 4);

        Link("Teflon Machine Polishing", "Teflon Polish", 1);
        Link("Teflon Machine Polishing", "Polishing Compound", 1);
        Link("Teflon Machine Polishing", "Polishing Pad", 1);
        Link("Teflon Machine Polishing", "Microfiber Cloth", 2);

        Link("Ceramic Water Wash", "Car Shampoo", 1);
        Link("Ceramic Water Wash", "Microfiber Cloth", 2);
        Link("Foam Wash", "Car Shampoo", 1);

        Link("Interior Cleaning", "Interior Cleaner", 1);
        Link("Interior Cleaning", "Dashboard Polish", 1);
        Link("Interior Cleaning", "Microfiber Cloth", 3);
        Link("Interior Cleaning", "Brush", 1);

        Link("Underbody Coating", "Underbody Coating Spray", 1);
        Link("Rain Repellent", "Rain Repellent Solution", 1);
        Link("Window Tinting / Sun Film", "Sun Control Film", 1);

        await db.SaveChangesAsync();
    }
}
