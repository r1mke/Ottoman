using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Identity;

namespace Ottoman.Infrastructure.Persistence;

public class OttomanDbContext(DbContextOptions<OttomanDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole, string>(options), Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext
{
    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<EmployeeService> EmployeeServices => Set<EmployeeService>();
    public DbSet<WorkingHours> WorkingHours => Set<WorkingHours>();
    public DbSet<TimeOff> TimeOff => Set<TimeOff>();
    public DbSet<SalonSettings> SalonSettings => Set<SalonSettings>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();
    public DbSet<ReservationSlot> ReservationSlots => Set<ReservationSlot>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys => Set<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>(e =>
        {
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
        });

        b.Entity<EmployeeProfile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasOne<AppUser>().WithOne().HasForeignKey<EmployeeProfile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Title).HasMaxLength(100);
            e.Property(x => x.Bio).HasMaxLength(500);
            e.HasMany(x => x.WorkingHours).WithOne().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.TimeOff).WithOne().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EmployeeService>(e =>
        {
            e.HasKey(x => new { x.EmployeeId, x.ServiceId });
            e.HasOne(x => x.Employee).WithMany(x => x.Services).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Service).WithMany(x => x.Employees).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<WorkingHours>().HasIndex(x => new { x.EmployeeId, x.DayOfWeek });
        b.Entity<TimeOff>().HasIndex(x => new { x.EmployeeId, x.StartUtc });

        b.Entity<SalonSettings>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.ToTable(t => t.HasCheckConstraint("CK_SalonSettings_SingleRow", "[Id] = 1"));
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.TimeZoneId).HasMaxLength(64);
            e.Property(x => x.Currency).HasMaxLength(3);
        });

        b.Entity<Service>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.ToTable(t => t.HasCheckConstraint("CK_Service_Duration", "[DurationMinutes] > 0 AND [DurationMinutes] % 5 = 0"));
        });

        b.Entity<Brand>(e => { e.Property(x => x.Name).HasMaxLength(100); e.HasIndex(x => x.Name).IsUnique(); });
        b.Entity<Category>(e => { e.Property(x => x.Name).HasMaxLength(100); e.HasIndex(x => x.Name).IsUnique(); });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.ToTable(t => t.HasCheckConstraint("CK_Product_Stock", "[Stock] >= 0"));
            e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Reservation>(e =>
        {
            e.Property(x => x.TotalPrice).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Slots).WithOne().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.EmployeeId, x.StartUtc });
            e.HasIndex(x => new { x.ClientId, x.StartUtc });
        });

        b.Entity<ReservationItem>(e =>
        {
            e.Property(x => x.ServiceName).HasMaxLength(150);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasOne<Service>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
        });

        // The database-level guarantee against double booking.
        b.Entity<ReservationSlot>().HasIndex(x => new { x.EmployeeId, x.SlotStartUtc }).IsUnique();

        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Provider).HasMaxLength(30);
            e.Property(x => x.ProviderRef).HasMaxLength(100);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.ClientSecret).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.ProviderRef).IsUnique();
        });

        b.Entity<Order>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Fulfilment).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DeliveryAddress).HasMaxLength(300);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.TotalPrice).HasPrecision(18, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ClientId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.Status, x.ExpiresUtc });
        });

        b.Entity<OrderItem>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(200);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_OrderItem_Quantity", "[Quantity] > 0"));
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(128);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(200);
            e.HasIndex(x => x.ProcessedAtUtc);
        });
    }
}
