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
    public DbSet<ShopSection> ShopSections => Set<ShopSection>();
    public DbSet<ShopSectionProduct> ShopSectionProducts => Set<ShopSectionProduct>();
    public DbSet<ShopSectionEntry> ShopSectionEntries => Set<ShopSectionEntry>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<HaircutCategory> HaircutCategories => Set<HaircutCategory>();
    public DbSet<GalleryItem> GalleryItems => Set<GalleryItem>();
    public DbSet<SalonOpeningHours> SalonOpeningHours => Set<SalonOpeningHours>();
    public DbSet<EmployeeTitle> EmployeeTitles => Set<EmployeeTitle>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();
    public DbSet<ReservationSlot> ReservationSlots => Set<ReservationSlot>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<ReservationReminder> ReservationReminders => Set<ReservationReminder>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys => Set<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>();

    /// <summary>
    /// SQL Server returns datetime2 with an unspecified kind, which serialises without a "Z" and which clients then
    /// read as local time. Every timestamp in this system is UTC, so say so when reading.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        builder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>(e =>
        {
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Bio).HasMaxLength(500);
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
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.MapsUrl).HasMaxLength(500);
            e.Property(x => x.InstagramUrl).HasMaxLength(300);
            e.Property(x => x.FacebookUrl).HasMaxLength(300);
            e.Property(x => x.LogoKey).HasMaxLength(200);
        });

        b.Entity<HaircutCategory>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<GalleryItem>(e =>
        {
            e.Property(x => x.ImageKey).HasMaxLength(200);
            e.Property(x => x.ContentHash).HasMaxLength(64);
            e.Property(x => x.Caption).HasMaxLength(200);
            e.Property(x => x.EmployeeId).HasMaxLength(450);
            e.HasIndex(x => x.ContentHash).IsUnique().HasFilter("[ContentHash] IS NOT NULL");
            e.HasIndex(x => new { x.CategoryId, x.CreatedAtUtc });
            e.HasIndex(x => x.IsFeatured);
            e.HasOne<HaircutCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<SalonOpeningHours>(e =>
        {
            e.HasKey(x => x.Day);
            e.Property(x => x.Day).ValueGeneratedNever();
            e.ToTable(t => t.HasCheckConstraint("CK_SalonOpeningHours_Range",
                "([OpenMinutes] IS NULL AND [CloseMinutes] IS NULL) OR ([OpenMinutes] >= 0 AND [CloseMinutes] <= 1440 AND [OpenMinutes] < [CloseMinutes])"));
        });

        b.Entity<Service>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.ToTable(t => t.HasCheckConstraint("CK_Service_Duration", "[DurationMinutes] > 0 AND [DurationMinutes] % 5 = 0"));
        });

        b.Entity<Brand>(e => { e.Property(x => x.Name).HasMaxLength(100); e.Property(x => x.LogoKey).HasMaxLength(300); e.HasIndex(x => x.Name).IsUnique(); });
        b.Entity<Category>(e => { e.Property(x => x.Name).HasMaxLength(100); e.HasIndex(x => x.Name).IsUnique(); });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.SalePrice).HasPrecision(18, 2);
            e.Ignore(x => x.EffectivePrice);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Product_Stock", "[Stock] >= 0");
                t.HasCheckConstraint("CK_Product_SalePrice", "[SalePrice] IS NULL OR ([SalePrice] > 0 AND [SalePrice] < [Price])");
            });
            e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ShopSection>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(100);
            e.Property(x => x.Eyebrow).HasMaxLength(50);
            e.Property(x => x.ImageKey).HasMaxLength(300);
            e.Property(x => x.Layout).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Background).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.TileSize).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CardSpan).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.HeadingAlign).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Products).WithOne().HasForeignKey(x => x.ShopSectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.ShopSectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.SortOrder);
        });
        b.Entity<ShopSectionEntry>(e => e.HasKey(x => new { x.ShopSectionId, x.EntityId }));
        b.Entity<ShopSectionProduct>(e =>
        {
            e.HasKey(x => new { x.ShopSectionId, x.ProductId });
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
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

        b.Entity<EmployeeProfile>().Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
        b.Entity<EmployeeProfile>().Property(x => x.PhotoZoom).HasDefaultValue(1.0);

        b.Entity<EmployeeTitle>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique(); // the default collation is case-insensitive
        });

        // The database-level guarantee against double booking.
        b.Entity<ReservationSlot>().HasIndex(x => new { x.EmployeeId, x.SlotStartUtc }).IsUnique();

        // ...and against one client sitting in two chairs at once. Filtered because rows from before this rule have
        // no client; cancelling removes the rows, so only live appointments hold a client's time.
        b.Entity<ReservationSlot>()
            .HasIndex(x => new { x.ClientId, x.SlotStartUtc })
            .IsUnique()
            .HasFilter("[ClientId] IS NOT NULL");

        // The same picture twice on one product is refused by the database too, not only by the pre-check.
        b.Entity<ProductImage>()
            .HasIndex(x => new { x.ProductId, x.ContentHash })
            .IsUnique()
            .HasFilter("[ContentHash] IS NOT NULL");

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

        b.Entity<Review>(e =>
        {
            e.Property(x => x.TargetType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.TargetKey).HasMaxLength(450);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.ReplyText).HasMaxLength(1000);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "[Rating] BETWEEN 1 AND 5"));
            // One review per client and target: the database enforces it, edits go through PUT.
            e.HasIndex(x => new { x.ClientId, x.TargetType, x.TargetKey }).IsUnique();
            e.HasIndex(x => new { x.TargetType, x.TargetKey, x.IsHidden });
        });

        b.Entity<IdempotencyRecord>(e =>
        {
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.Key).HasMaxLength(64);
            e.Property(x => x.Route).HasMaxLength(200);
            e.Property(x => x.RequestHash).HasMaxLength(64);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.HasIndex(x => new { x.UserId, x.Key }).IsUnique();
            e.HasIndex(x => x.CreatedAtUtc);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.UserName).HasMaxLength(200);
            e.Property(x => x.Role).HasMaxLength(20);
            e.Property(x => x.Method).HasMaxLength(10);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.Route).HasMaxLength(200);
            e.Property(x => x.Target).HasMaxLength(200);
            e.Property(x => x.Ip).HasMaxLength(64);
            // History must not vanish with an account, so deleting a user who has audit lines is refused.
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.AtUtc);
            e.HasIndex(x => new { x.UserId, x.AtUtc });
        });

        b.Entity<DeviceToken>(e =>
        {
            e.Property(x => x.Token).HasMaxLength(512);
            e.Property(x => x.Platform).HasMaxLength(20);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        b.Entity<UserNotification>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Body).HasMaxLength(2000);
            e.Property(x => x.Kind).HasMaxLength(50);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Announcement>().WithMany().HasForeignKey(x => x.AnnouncementId).OnDelete(DeleteBehavior.SetNull);
            // The inbox query: a user's newest messages, unread first when filtered.
            e.HasIndex(x => new { x.UserId, x.ReadAtUtc, x.CreatedAtUtc });
        });

        b.Entity<Announcement>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Body).HasMaxLength(2000);
            e.Property(x => x.ImageKey).HasMaxLength(300);
            e.Property(x => x.Audience).HasConversion<string>().HasMaxLength(20);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ReservationReminder>(e =>
        {
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
            e.HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Cascade);
            // The database guarantees at most one reminder of each kind per appointment.
            e.HasIndex(x => new { x.ReservationId, x.Kind }).IsUnique();
        });

        b.Entity<ProcessedEvent>(e =>
        {
            e.HasKey(x => x.OutboxMessageId);
            e.Property(x => x.OutboxMessageId).ValueGeneratedNever();
        });

        b.Entity<SentEmail>(e =>
        {
            e.HasKey(x => new { x.OutboxMessageId, x.RecipientKey });
            e.Property(x => x.RecipientKey).HasMaxLength(64);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(128);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.SessionId);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(200);
            e.HasIndex(x => x.ProcessedAtUtc);
        });
    }
}

public class UtcDateTimeConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

public class NullableUtcDateTimeConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue && v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
