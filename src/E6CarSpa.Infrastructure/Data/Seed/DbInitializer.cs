using System.Security.Cryptography;
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

        // Backfill: Permissions arrives as 0 (None) on existing rows, which would lock everyone
        // out. Give each account the preset for the role it already has.
        var unpermissioned = await db.Users.Where(u => u.Permissions == Permission.None).ToListAsync();
        if (unpermissioned.Count > 0)
        {
            foreach (var u in unpermissioned) u.Permissions = PermissionPresets.For(u.Role);
            await db.SaveChangesAsync();
            logger?.LogInformation("Backfilled permissions for {Count} existing user(s) from their role.",
                unpermissioned.Count);
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
            // No shipped default password: generate a random one and hand it to the operator once.
            // MustChangePassword forces them to replace it before the account can do anything else.
            var generated = GeneratePassword();
            db.Users.Add(new User
            {
                FullName = "Administrator",
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(generated),
                Role = UserRole.Admin,
                Permissions = Permission.All,
                IsActive = true,
                MustChangePassword = true
            });
            await db.SaveChangesAsync();
            PublishCredential(logger, "admin", generated, "first-run administrator account created");
        }
        else
        {
            // Older installs were seeded with the well-known password 'admin@123'. Leaving it in
            // place is a critical hole once the API is reachable from the Internet, and a startup
            // warning is easy to miss — so retire it automatically: rotate to a random password,
            // force a change on next login, and invalidate any token issued under the old one.
            await RetireKnownDefaultPasswordsAsync(db, logger);
        }

        if (!await db.Services.AnyAsync())
            db.Services.AddRange(SeedServices());

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Passwords this application shipped as defaults in earlier versions. Any account still using
    /// one is treated as compromised: they are published in the source history and in this file.
    /// </summary>
    private static readonly string[] RetiredDefaultPasswords = ["admin@123"];

    /// <summary>
    /// Rotate any account still using a shipped default password onto a fresh random one, force a
    /// change at next login, and revoke existing tokens. Runs on every startup and is a no-op once
    /// no account matches — so upgrading an old install self-heals without operator action.
    /// </summary>
    private static async Task RetireKnownDefaultPasswordsAsync(AppDbContext db, ILogger? logger)
    {
        // Only active accounts can log in, so only those are worth rotating.
        var users = await db.Users.Where(u => u.IsActive).ToListAsync();

        foreach (var user in users)
        {
            if (!RetiredDefaultPasswords.Any(p => BCrypt.Net.BCrypt.Verify(p, user.PasswordHash)))
                continue;

            var generated = GeneratePassword();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(generated);
            user.MustChangePassword = true;
            // Anyone holding a token minted with the default password loses it immediately.
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.FailedLoginCount = 0;
            user.LockoutEndAt = null;

            await db.SaveChangesAsync();
            PublishCredential(logger, user.Username, generated,
                "the previous password was a known shipped default and has been retired");
        }
    }

    /// <summary>
    /// Cryptographically random password. Excludes characters that are easy to misread when
    /// someone is copying it off a screen (0/O, 1/l/I) since it is transcribed by hand once.
    /// </summary>
    private static string GeneratePassword(int length = 20)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789@#%+=?";
        var bytes = RandomNumberGenerator.GetBytes(length);
        return string.Concat(bytes.Select(b => alphabet[b % alphabet.Length]));
    }

    /// <summary>
    /// Surface a generated credential to the operator: prominently in the log, and in a file beside
    /// the application for when the log has already scrolled past (a service install writes to a log
    /// nobody watches). The file is the operator's copy to delete once the password is changed.
    /// </summary>
    private static void PublishCredential(ILogger? logger, string username, string password, string reason)
    {
        logger?.LogWarning(
            "SECURITY: password for '{Username}' was generated because {Reason}. " +
            "It is temporary — the account cannot do anything until this password is changed. " +
            "Username: {Username}  Password: {Password}",
            username, reason, username, password);

        try
        {
            // The Linux unit runs with ProtectSystem=strict, so the install directory is read-only
            // there; E6_STATE_DIR points at the one writable path. Falls back to the app directory,
            // which is what the Windows service (LocalSystem) uses.
            var dir = Environment.GetEnvironmentVariable("E6_STATE_DIR");
            if (string.IsNullOrWhiteSpace(dir)) dir = AppContext.BaseDirectory;
            var path = Path.Combine(dir, "FIRST-RUN-ADMIN-PASSWORD.txt");
            File.WriteAllText(path,
                $"""
                E6 Car Spa — temporary credential
                Generated: {DateTime.Now:yyyy-MM-dd HH:mm}
                Reason:    {reason}

                Username:  {username}
                Password:  {password}

                This password only allows one action: setting a new password. Sign in, change it,
                then DELETE this file.
                """);
            logger?.LogWarning("The credential was also written to {Path} — delete it once the password is changed.", path);
        }
        catch (Exception ex)
        {
            // A read-only install directory must not stop the app booting; the log still has it.
            logger?.LogWarning(ex, "Could not write the credential file; use the password from the log above.");
        }
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
