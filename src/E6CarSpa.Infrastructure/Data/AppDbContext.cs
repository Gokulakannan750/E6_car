using E6CarSpa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ServiceProduct> ServiceProducts => Set<ServiceProduct>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Money: use numeric(12,2) everywhere for predictable rounding.
        foreach (var prop in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetColumnType("numeric(12,2)");
        }

        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(50).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
        });

        b.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.Phone);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        });

        b.Entity<Vehicle>(e =>
        {
            e.HasIndex(x => x.CarNumber);
            e.Property(x => x.CarNumber).HasMaxLength(100).IsRequired();
            e.Property(x => x.CarModel).HasMaxLength(120);
            e.HasOne(x => x.Customer).WithMany(c => c.Vehicles)
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Service>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Category).HasMaxLength(60);
            e.Property(x => x.HsnSac).HasMaxLength(10);
        });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Category).HasMaxLength(60);
            // Stock quantities may be fractional (ml, metres) -> wider scale than money.
            e.Property(x => x.StockQuantity).HasColumnType("numeric(12,3)");
            e.Property(x => x.ReorderLevel).HasColumnType("numeric(12,3)");
        });

        b.Entity<ServiceProduct>(e =>
        {
            e.Property(x => x.DefaultQuantity).HasColumnType("numeric(12,3)");
            e.HasOne(x => x.Service).WithMany(s => s.BillOfMaterials)
                .HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany()
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StockMovement>(e =>
        {
            e.Property(x => x.Quantity).HasColumnType("numeric(12,3)");
            e.HasOne(x => x.Product).WithMany(p => p.Movements)
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Invoice).WithMany()
                .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Invoice>(e =>
        {
            e.Ignore(x => x.Balance); // computed in C#, not stored
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt); // reports / dashboard filter by date
            e.Property(x => x.InvoiceNumber).HasMaxLength(40);
            e.HasOne(x => x.Customer).WithMany(c => c.Invoices)
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Vehicle).WithMany(v => v.Invoices)
                .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<InvoiceItem>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(200).IsRequired();
            e.Property(x => x.Quantity).HasColumnType("numeric(12,3)");
            e.HasOne(x => x.Invoice).WithMany(i => i.Items)
                .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Service).WithMany()
                .HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Product).WithMany()
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.PaidAt); // reports / dashboard filter collections by date
            e.HasOne(x => x.Invoice).WithMany(i => i.Payments)
                .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<NotificationLog>(e =>
        {
            e.Property(x => x.ToPhone).HasMaxLength(20);
            e.Property(x => x.TemplateName).HasMaxLength(80);
            e.HasOne(x => x.Invoice).WithMany()
                .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
