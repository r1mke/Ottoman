using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ottoman.Application.Images;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Seeding;

/// <summary>
/// Gives every demo product, service, barber and news item a picture, stored where all pictures live (Cloudinary).
/// Runs only when a real account is configured and only for things that have no picture yet, so it is safe on every
/// start; a failure (no network, wrong key) is logged and never stops the application.
/// </summary>
public class DemoImageSeeder(OttomanDbContext db, IImageStore store, IOptions<ImageOptions> options, ILogger<DemoImageSeeder> log)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!options.Value.Configured) return;

        try
        {
            var done = 0;

            foreach (var p in await db.Products.Where(p => !db.ProductImages.Any(i => i.ProductId == p.Id)).ToListAsync(ct))
            {
                db.ProductImages.Add(new ProductImage { ProductId = p.Id, FileKey = await UploadAsync(p.Name, "ottoman/products", ct) });
                done++;
            }
            foreach (var s in await db.Services.Where(s => s.ImageKey == null).ToListAsync(ct))
            {
                s.ImageKey = await UploadAsync(s.Name, "ottoman/services", ct);
                done++;
            }
            foreach (var e in await db.EmployeeProfiles.Where(e => e.PhotoKey == null).ToListAsync(ct))
            {
                e.PhotoKey = await UploadAsync(e.UserId + e.Title, "ottoman/employees", ct);
                done++;
            }

            await db.SaveChangesAsync(ct);
            if (done > 0) log.LogInformation("Seeded {Count} demo pictures", done);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Demo pictures could not be uploaded; the data is fine, the pictures are simply missing");
        }
    }

    private async Task<string> UploadAsync(string label, string folder, CancellationToken ct)
    {
        await using var content = new MemoryStream(PlaceholderImage.Create(label));
        return await store.UploadAsync(content, folder, ct);
    }
}
