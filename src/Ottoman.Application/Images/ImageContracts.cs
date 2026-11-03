using Ottoman.Application.Common;

namespace Ottoman.Application.Images;

public class ImageOptions
{
    public const string Section = "Cloudinary";

    public string CloudName { get; set; } = "";
    public string ApiKey { get; set; } = "";

    /// <summary>Signs every upload and delete. Never leaves the server.</summary>
    public string ApiSecret { get; set; } = "";

    public long MaxBytes { get; set; } = 5 * 1024 * 1024;
    public int MaxImagesPerProduct { get; set; } = 8;

    public bool Configured => CloudName != "" && ApiKey != "" && ApiSecret != "";
}

/// <summary>Where pictures live. Only the key (public id) is stored in our database; the URL is derived from it.</summary>
public interface IImageStore
{
    /// <summary>Stores the bytes under the folder and returns the key.</summary>
    Task<string> UploadAsync(Stream content, string folder, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>Attaches uploaded pictures to products, services and employees. Admin-only at the HTTP edge.</summary>
public interface IImageAttachments
{
    Task<Result<bool>> AddProductImageAsync(int productId, Stream content, CancellationToken ct);
    Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, CancellationToken ct);
    /// <summary>Adds a picture to the work gallery; the result is the new item's id.</summary>
    Task<Result<int>> AddGalleryItemAsync(int categoryId, string? employeeId, string? caption, bool featured, Stream content, CancellationToken ct);
    Task<Result<bool>> RemoveGalleryItemAsync(int id, CancellationToken ct);
    /// <summary>Uploads the picture of a news item that is about to be created and returns its storage key.</summary>
    Task<Result<string>> UploadAnnouncementImageAsync(Stream content, CancellationToken ct);

    /// <summary>Deletes an uploaded picture that was never attached (the creation failed afterwards).</summary>
    Task DiscardImageAsync(string key);
    Task<Result<bool>> SetBrandLogoAsync(int brandId, Stream content, CancellationToken ct);
    Task<Result<bool>> ClearBrandLogoAsync(int brandId, CancellationToken ct);

    /// <summary>Uploads the picture of a shop section card (it replaces a picked product picture, if any).</summary>
    Task<Result<bool>> SetShopSectionImageAsync(int sectionId, Stream content, CancellationToken ct);

    /// <summary>Removes the card picture, uploaded or picked: the card goes back to its automatic cover.</summary>
    Task<Result<bool>> ClearShopSectionImageAsync(int sectionId, CancellationToken ct);
    Task<Result<bool>> SetSalonLogoAsync(Stream content, CancellationToken ct);
    Task<Result<bool>> ClearSalonLogoAsync(CancellationToken ct);
    Task<Result<bool>> SetServiceImageAsync(int serviceId, Stream content, CancellationToken ct);
    Task<Result<bool>> ClearServiceImageAsync(int serviceId, CancellationToken ct);
    /// <param name="onAccount">The caller's own picture when they have no barber profile (the admin): kept on the account.</param>
    Task<Result<bool>> SetEmployeePhotoAsync(string employeeId, Stream content, CancellationToken ct, bool onAccount = false);
    Task<Result<bool>> ClearEmployeePhotoAsync(string employeeId, CancellationToken ct, bool onAccount = false);
}

public static class ImageRules
{
    public const string Transform = "c_limit,w_1200,q_auto,f_auto";

    public static string? DeliveryUrl(string? cloudName, string? key) =>
        string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(cloudName)
            ? null
            : $"https://res.cloudinary.com/{cloudName}/image/upload/{Transform}/{key}";

    public const double MinZoom = 1.0;
    public const double MaxZoom = 2.0;

    public static bool ValidZoom(double? zoom) => zoom is null || (zoom >= MinZoom && zoom <= MaxZoom);

    /// <summary>
    /// A square avatar centred on the face Cloudinary detects. Zoom 1..2 is how close to crop (Cloudinary's
    /// <c>z_</c> is the inverse: smaller is closer); without a face it falls back to the middle of the picture.
    /// The <c>z_</c> part is always present so a client can swap it for a live preview.
    /// </summary>
    public static string? AvatarUrl(string? cloudName, string? key, double zoom)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(cloudName)) return null;
        var z = (1.0 / Math.Clamp(zoom, MinZoom, MaxZoom)).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return $"https://res.cloudinary.com/{cloudName}/image/upload/c_thumb,g_face,w_512,h_512,z_{z},q_auto,f_auto/{key}";
    }

    /// <summary>
    /// Decides from the first bytes what the file really is. The declared content type and file name come from the
    /// client and are ignored. Returns null when it is not a JPEG, PNG or WebP picture.
    /// </summary>
    public static string? Detect(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return "image/jpeg";
        if (head.Length >= 8 && head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    /// <summary>Returns the problem in Bosnian, or null when the upload is acceptable.</summary>
    public static string? Check(long length, ReadOnlySpan<byte> head, long maxBytes)
    {
        if (length <= 0) return "Slika je prazna.";
        if (length > maxBytes) return $"Slika je prevelika (najviše {maxBytes / (1024 * 1024)} MB).";
        return Detect(head) is null ? "Dozvoljene su samo JPEG, PNG i WebP slike." : null;
    }
}
