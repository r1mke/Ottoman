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

        var upload = await UploadAsync(content, "ottoman/products", ct);
        if (upload.Error is not null) return Result<bool>.Invalid(upload.Error);

        try
        {
            db.ProductImages.Add(new ProductImage
            {
                ProductId = productId,
                FileKey = upload.Key!,
                SortOrder = existing.Count == 0 ? 0 : existing.Max() + 1,
            });
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await TryDeleteAsync(upload.Key!);
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

    public Task<Result<bool>> SetEmployeePhotoAsync(string employeeId, Stream content, CancellationToken ct) =>
        ReplaceEmployeeAsync(employeeId, content, ct);

    public Task<Result<bool>> ClearEmployeePhotoAsync(string employeeId, CancellationToken ct) =>
        ReplaceEmployeeAsync(employeeId, null, ct);

    private Task<Result<bool>> ReplaceEmployeeAsync(string employeeId, Stream? content, CancellationToken ct) =>
        ReplaceAsync(
            async () => await db.EmployeeProfiles.Where(p => p.UserId == employeeId).Select(p => new KeyRow(p.PhotoKey)).FirstOrDefaultAsync(ct),
            key => db.EmployeeProfiles.Where(p => p.UserId == employeeId).ExecuteUpdateAsync(s => s.SetProperty(x => x.PhotoKey, key), ct),
            content, "ottoman/employees", ct);

    // ------------------------------------------------------------ announcements

    public Task<Result<bool>> SetAnnouncementImageAsync(int announcementId, Stream content, CancellationToken ct) =>
        ReplaceAnnouncementAsync(announcementId, content, ct);

    public Task<Result<bool>> ClearAnnouncementImageAsync(int announcementId, CancellationToken ct) =>
        ReplaceAnnouncementAsync(announcementId, null, ct);

    private Task<Result<bool>> ReplaceAnnouncementAsync(int announcementId, Stream? content, CancellationToken ct) =>
        ReplaceAsync(
            async () => await db.Announcements.Where(a => a.Id == announcementId).Select(a => new KeyRow(a.ImageKey)).FirstOrDefaultAsync(ct),
            key => db.Announcements.Where(a => a.Id == announcementId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ImageKey, key), ct),
            content, "ottoman/news", ct);

    // ----------------------------------------------------------------- helpers

    private sealed record KeyRow(string? Key);

    private sealed record UploadOutcome(string? Key, string? Error);

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
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > Options.MaxBytes) break;
        }

        var head = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16));
        var problem = ImageRules.Check(buffer.Length, head, Options.MaxBytes);
        if (problem is not null) return new UploadOutcome(null, problem);

        buffer.Position = 0;
        return new UploadOutcome(await store.UploadAsync(buffer, folder, ct), null);
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
