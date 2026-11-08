using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ottoman.Application.Common;
using Ottoman.Application.Images;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Images;

public class ImageAttachments(
    OttomanDbContext db,
    IImageStore store,
    IOptions<ImageOptions> options,
    ILogger<ImageAttachments> log) : IImageAttachments
{
    private ImageOptions Options => options.Value;

    // ---------------------------------------------------------------- products

    public async Task<Result<bool>> AddProductImageAsync(int productId, Stream content, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct)) return Result<bool>.NotFound();

        // Two simultaneous uploads can overshoot the cap by one; the cap protects the account, it is not an invariant.
        var existing = await db.ProductImages.Where(i => i.ProductId == productId).Select(i => i.SortOrder).ToListAsync(ct);
        if (existing.Count >= Options.MaxImagesPerProduct)
            return Result<bool>.Conflict($"Proizvod može imati najviše {Options.MaxImagesPerProduct} slika.");

        var prepared = await PrepareAsync(content, ct);
        if (prepared.Error is not null) return Result<bool>.Invalid(prepared.Error);
        using var buffer = prepared.Buffer!;

        if (await db.ProductImages.AnyAsync(i => i.ProductId == productId && i.ContentHash == prepared.Hash, ct))
            return Result<bool>.Conflict(SamePictureMessage);

        var stored = await StoreAsync(buffer, "ottoman/products", ct);
        if (stored.Error is not null) return Result<bool>.Invalid(stored.Error);
        var key = stored.Key!;
        try
        {
            db.ProductImages.Add(new ProductImage
            {
                ProductId = productId,
                FileKey = key,
                ContentHash = prepared.Hash,
                SortOrder = existing.Count == 0 ? 0 : existing.Max() + 1,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A parallel upload of the same picture got there first.
            await TryDeleteAsync(key);
            return Result<bool>.Conflict(SamePictureMessage);
        }
        catch
        {
            await TryDeleteAsync(key);
            throw;
        }
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, CancellationToken ct)
    {
        var image = await db.ProductImages.AsNoTracking().FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, ct);
        if (image is null) return Result<bool>.NotFound();
        var rows = await db.ProductImages.Where(i => i.Id == imageId).ExecuteDeleteAsync(ct);
        if (rows == 1) await TryDeleteAsync(image.FileKey);
        return Result<bool>.Ok(true);
    }

    // ----------------------------------------------------------------- gallery

    public async Task<Result<int>> AddGalleryItemAsync(
        int categoryId, string? employeeId, string? caption, bool featured, Stream content, CancellationToken ct)
    {
        if (!await db.HaircutCategories.AnyAsync(c => c.Id == categoryId, ct))
            return Result<int>.Invalid("Odabrana kategorija frizure ne postoji.");
        if (employeeId is not null && !await db.EmployeeProfiles.AnyAsync(p => p.UserId == employeeId, ct))
            return Result<int>.Invalid("Odabrani uposlenik ne postoji.");

        var prepared = await PrepareAsync(content, ct);
        if (prepared.Error is not null) return Result<int>.Invalid(prepared.Error);
        using var buffer = prepared.Buffer!;

        if (await db.GalleryItems.AnyAsync(i => i.ContentHash == prepared.Hash, ct))
            return Result<int>.Conflict(SamePictureMessage);

        var stored = await StoreAsync(buffer, "ottoman/gallery", ct);
        if (stored.Error is not null) return Result<int>.Invalid(stored.Error);
        var key = stored.Key!;
        try
        {
            var item = new GalleryItem
            {
                ImageKey = key,
                ContentHash = prepared.Hash,
                CategoryId = categoryId,
                EmployeeId = employeeId,
                Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
                IsFeatured = featured,
            };
            db.GalleryItems.Add(item);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(item.Id);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await TryDeleteAsync(key);
            return Result<int>.Conflict(SamePictureMessage);
        }
        catch
        {
            await TryDeleteAsync(key);
            throw;
        }
    }

    public async Task<Result<bool>> RemoveGalleryItemAsync(int id, CancellationToken ct)
    {
        var item = await db.GalleryItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return Result<bool>.NotFound();
        var rows = await db.GalleryItems.Where(i => i.Id == id).ExecuteDeleteAsync(ct);
        if (rows == 1) await TryDeleteAsync(item.ImageKey);
        return Result<bool>.Ok(true);
    }

    // ------------------------------------------------------------- salon logo

    public Task<Result<bool>> SetSalonLogoAsync(Stream content, CancellationToken ct) => ReplaceLogoAsync(content, ct);

    public Task<Result<bool>> ClearSalonLogoAsync(CancellationToken ct) => ReplaceLogoAsync(null, ct);

    private Task<Result<bool>> ReplaceLogoAsync(Stream? content, CancellationToken ct) =>
        ReplaceAsync(
            async () => await db.SalonSettings.Select(s => new KeyRow(s.LogoKey)).FirstOrDefaultAsync(ct),
            key => db.SalonSettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.LogoKey, key), ct),
            content, "ottoman/salon", ct);

    // ---------------------------------------------------------------- news items

    public async Task<Result<string>> UploadAnnouncementImageAsync(Stream content, CancellationToken ct)
    {
        var prepared = await PrepareAsync(content, ct);
        if (prepared.Error is not null) return Result<string>.Invalid(prepared.Error);
        using var buffer = prepared.Buffer!;
        var stored = await StoreAsync(buffer, "ottoman/announcements", ct);
        return stored.Error is not null ? Result<string>.Invalid(stored.Error) : Result<string>.Ok(stored.Key!);
    }

    public Task DiscardImageAsync(string key) => TryDeleteAsync(key);

    // ------------------------------------------------------------ brand logos

    public Task<Result<bool>> SetBrandLogoAsync(int brandId, Stream content, CancellationToken ct) =>
        ReplaceBrandLogoAsync(brandId, content, ct);

    public Task<Result<bool>> ClearBrandLogoAsync(int brandId, CancellationToken ct) =>
        ReplaceBrandLogoAsync(brandId, null, ct);

    private Task<Result<bool>> ReplaceBrandLogoAsync(int brandId, Stream? content, CancellationToken ct) =>
        ReplaceAsync(
            async () => await db.Brands.Where(b => b.Id == brandId).Select(b => new KeyRow(b.LogoKey)).FirstOrDefaultAsync(ct),
            key => db.Brands.Where(b => b.Id == brandId).ExecuteUpdateAsync(s => s.SetProperty(x => x.LogoKey, key), ct),
            content, "ottoman/brands", ct);

    // --------------------------------------------------------- shop section cards

    public Task<Result<bool>> SetShopSectionImageAsync(int sectionId, Stream content, CancellationToken ct) =>
        ReplaceSectionImageAsync(sectionId, content, ct);

    public Task<Result<bool>> ClearShopSectionImageAsync(int sectionId, CancellationToken ct) =>
        ReplaceSectionImageAsync(sectionId, null, ct);

    private Task<Result<bool>> ReplaceSectionImageAsync(int sectionId, Stream? content, CancellationToken ct) =>
        ReplaceAsync(
            async () => await db.ShopSections.Where(s => s.Id == sectionId).Select(s => new KeyRow(s.ImageKey)).FirstOrDefaultAsync(ct),
            // An uploaded picture and a picked product picture exclude each other.
            key => db.ShopSections.Where(s => s.Id == sectionId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ImageKey, key).SetProperty(x => x.ImageProductImageId, (int?)null), ct),
            content, "ottoman/shop", ct);

    // ---------------------------------------------------------------- services

    public Task<Result<bool>> SetServiceImageAsync(int serviceId, Stream content, CancellationToken ct) =>
        ReplaceServiceAsync(serviceId, content, ct);

    public Task<Result<bool>> ClearServiceImageAsync(int serviceId, CancellationToken ct) =>
        ReplaceServiceAsync(serviceId, null, ct);

    private Task<Result<bool>> ReplaceServiceAsync(int serviceId, Stream? content, CancellationToken ct) =>
        ReplaceAsync(
            async () => await db.Services.Where(s => s.Id == serviceId).Select(s => new KeyRow(s.ImageKey)).FirstOrDefaultAsync(ct),
            key => db.Services.Where(s => s.Id == serviceId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ImageKey, key), ct),
            content, "ottoman/services", ct);

    // --------------------------------------------------------------- employees

    public Task<Result<bool>> SetEmployeePhotoAsync(
        string employeeId, Stream content, CancellationToken ct, bool onAccount = false, double? zoom = null) =>
        ReplaceEmployeeAsync(employeeId, content, onAccount, zoom ?? ImageRules.MinZoom, ct);

    public Task<Result<bool>> ClearEmployeePhotoAsync(string employeeId, CancellationToken ct, bool onAccount = false) =>
        ReplaceEmployeeAsync(employeeId, null, onAccount, ImageRules.MinZoom, ct);

    /// <summary>A new picture starts from the zoom sent with it (default 1); removing it resets the zoom to 1.</summary>
    private async Task<Result<bool>> ReplaceEmployeeAsync(
        string employeeId, Stream? content, bool onAccount, double zoom, CancellationToken ct)
    {
        // An account without a barber profile (a client, the admin) keeps its picture on the account itself.
        if (onAccount && !await db.EmployeeProfiles.AnyAsync(p => p.UserId == employeeId, ct))
            return await ReplaceAsync(
                async () => await db.Users.Where(u => u.Id == employeeId).Select(u => new KeyRow(u.PhotoKey)).FirstOrDefaultAsync(ct),
                key => db.Users.Where(u => u.Id == employeeId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PhotoKey, key).SetProperty(x => x.PhotoZoom, zoom), ct),
                content, "ottoman/accounts", ct);
        return await ReplaceAsync(
            async () => await db.EmployeeProfiles.Where(p => p.UserId == employeeId).Select(p => new KeyRow(p.PhotoKey)).FirstOrDefaultAsync(ct),
            key => db.EmployeeProfiles.Where(p => p.UserId == employeeId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PhotoKey, key).SetProperty(x => x.PhotoZoom, zoom), ct),
            content, "ottoman/employees", ct);
    }

    public async Task<Result<bool>> SetOwnPhotoZoomAsync(string userId, double zoom, CancellationToken ct)
    {
        // One conditional statement each: the zoom only lands on a picture that is still there.
        var rows = await db.EmployeeProfiles.Where(p => p.UserId == userId && p.PhotoKey != null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PhotoZoom, zoom), ct);
        if (rows == 1) return Result<bool>.Ok(true);
        if (await db.EmployeeProfiles.AnyAsync(p => p.UserId == userId, ct)) return Result<bool>.Conflict(NoPictureMessage);

        rows = await db.Users.Where(u => u.Id == userId && u.PhotoKey != null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PhotoZoom, zoom), ct);
        if (rows == 1) return Result<bool>.Ok(true);
        return await db.Users.AnyAsync(u => u.Id == userId, ct)
            ? Result<bool>.Conflict(NoPictureMessage)
            : Result<bool>.NotFound();
    }

    // ----------------------------------------------------------------- helpers

    private sealed record KeyRow(string? Key);

    private sealed record UploadOutcome(string? Key, string? Error);

    private sealed record Prepared(MemoryStream? Buffer, string? Hash, string? Error);

    private const string SamePictureMessage = "Ta slika je već dodana. Odaberite drugu.";

    private const string NoPictureMessage = "Prvo dodajte sliku.";

    /// <summary>Upload first, switch the column in one statement, then delete the old picture (a failure only leaves an orphan).</summary>
    private async Task<Result<bool>> ReplaceAsync(
        Func<Task<KeyRow?>> current, Func<string?, Task<int>> write, Stream? content, string folder, CancellationToken ct)
    {
        var row = await current();
        if (row is null) return Result<bool>.NotFound();

        string? newKey = null;
        if (content is not null)
        {
            var upload = await UploadAsync(content, folder, ct);
            if (upload.Error is not null) return Result<bool>.Invalid(upload.Error);
            newKey = upload.Key;
        }

        int rows;
        try
        {
            rows = await write(newKey);
        }
        catch
        {
            if (newKey is not null) await TryDeleteAsync(newKey);
            throw;
        }

        if (rows != 1)
        {
            if (newKey is not null) await TryDeleteAsync(newKey);
            return Result<bool>.NotFound();
        }
        if (row.Key is not null && row.Key != newKey) await TryDeleteAsync(row.Key);
        return Result<bool>.Ok(true);
    }

    /// <summary>Reads at most the allowed size, checks the real file type from its first bytes, then stores it.</summary>
    private async Task<UploadOutcome> UploadAsync(Stream content, string folder, CancellationToken ct)
    {
        var prepared = await PrepareAsync(content, ct);
        if (prepared.Error is not null) return new UploadOutcome(null, prepared.Error);
        using var buffer = prepared.Buffer!;
        return await StoreAsync(buffer, folder, ct);
    }

    /// <summary>Sends checked bytes to the store; a file the image host cannot read comes back as an error, not a 500.</summary>
    private async Task<UploadOutcome> StoreAsync(MemoryStream buffer, string folder, CancellationToken ct)
    {
        try
        {
            return new UploadOutcome(await store.UploadAsync(buffer, folder, ct), null);
        }
        catch (ImageRejectedException e)
        {
            log.LogInformation("The image store refused an upload to {Folder} as not a valid picture", folder);
            return new UploadOutcome(null, e.Message);
        }
    }

    /// <summary>
    /// Reads at most the allowed size, checks the real file type from its first bytes and that the file is a whole
    /// picture (not just a header or a cut-off download). The caller owns the buffer.
    /// </summary>
    private async Task<Prepared> PrepareAsync(Stream content, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > Options.MaxBytes) break;
        }

        var problem = ImageRules.CheckFile(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), Options.MaxBytes);
        if (problem is not null)
        {
            buffer.Dispose();
            return new Prepared(null, null, problem);
        }

        var hash = Convert.ToHexString(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)));
        buffer.Position = 0;
        return new Prepared(buffer, hash, null);
    }

    private async Task TryDeleteAsync(string key)
    {
        try
        {
            await store.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not delete image {Key} from the store; it is now an orphan", key);
        }
    }
}
