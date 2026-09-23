using CarShell.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Data;

public class CarShellDbContext(DbContextOptions<CarShellDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<ListingStatusEvent> ListingStatusEvents => Set<ListingStatusEvent>();
    public DbSet<SellerSubscription> SellerSubscriptions => Set<SellerSubscription>();
    public DbSet<Make> Makes => Set<Make>();
    public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
    public DbSet<Suburb> Suburbs => Set<Suburb>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(u => u.Email).IsUnique();
            // Postgres unique indexes treat NULLs as distinct, so this only
            // enforces uniqueness among admins who actually set a username.
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Make>(e =>
        {
            e.HasIndex(m => m.Name).IsUnique();
        });

        modelBuilder.Entity<VehicleModel>(e =>
        {
            e.HasOne(m => m.Make).WithMany(mk => mk.Models).HasForeignKey(m => m.MakeId);
            e.HasIndex(m => new { m.MakeId, m.Name }).IsUnique();
        });

        modelBuilder.Entity<Suburb>(e =>
        {
            e.HasIndex(s => new { s.City, s.Name }).IsUnique();
        });

        modelBuilder.Entity<Listing>(e =>
        {
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.FuelType).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.Transmission).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.BodyType).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.EngineCapacityLitres).HasColumnType("decimal(3,1)");
            e.Property(l => l.Price).HasColumnType("decimal(10,2)");
            e.Property(l => l.Vin).HasMaxLength(17);
            e.Property(l => l.RegistrationNumber).HasMaxLength(20);
            e.Property(l => l.Location).HasColumnType("geography (point)");
            e.Property(l => l.Version).IsRowVersion();

            e.HasOne(l => l.Seller).WithMany(u => u.Listings).HasForeignKey(l => l.SellerId);
            e.HasOne(l => l.Make).WithMany().HasForeignKey(l => l.MakeId);
            e.HasOne(l => l.Model).WithMany().HasForeignKey(l => l.ModelId);
            e.HasOne(l => l.Suburb).WithMany().HasForeignKey(l => l.SuburbId);

            // Composite indexes matching the filters the Distance + Price
            // Filtering section of the design doc actually queries by.
            e.HasIndex(l => new { l.Status, l.Price });
            e.HasIndex(l => new { l.Status, l.Year });
            e.HasIndex(l => new { l.Status, l.EngineCapacityLitres });
            e.HasIndex(l => new { l.Status, l.MakeId, l.ModelId });
            e.HasIndex(l => l.Location).HasMethod("GIST");

            // A duplicate VIN from the same seller is rejected outright
            // rather than silently creating a second listing for the same car.
            e.HasIndex(l => new { l.SellerId, l.Vin }).IsUnique();
        });

        modelBuilder.Entity<ListingImage>(e =>
        {
            e.HasOne(i => i.Listing).WithMany(l => l.Images).HasForeignKey(i => i.ListingId);
            e.HasIndex(i => new { i.ListingId, i.Position });
        });

        modelBuilder.Entity<ListingStatusEvent>(e =>
        {
            e.Property(s => s.FromStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.ToStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.SalePrice).HasColumnType("decimal(10,2)");

            e.HasOne(s => s.Listing).WithMany(l => l.StatusEvents).HasForeignKey(s => s.ListingId);
            e.HasOne(s => s.ChangedByUser).WithMany().HasForeignKey(s => s.ChangedBy);

            e.HasIndex(s => new { s.ListingId, s.ChangedAt });
            // Backs the "cars sold in March" style reporting query.
            e.HasIndex(s => new { s.ToStatus, s.ChangedAt });
        });

        modelBuilder.Entity<SellerSubscription>(e =>
        {
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(s => s.Seller).WithMany().HasForeignKey(s => s.SellerId);
            e.HasIndex(s => s.SellerId);
        });
    }
}
