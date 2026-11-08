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

    /// <summary>Replaces (stream) or removes (null) the picture of a sent announcement.</summary>
    Task<Result<bool>> ReplaceAnnouncementImageAsync(int announcementId, Stream? content, CancellationToken ct);

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
    /// <param name="onAccount">The caller's own picture when they have no barber profile (a client, the admin): kept on the account.</param>
    /// <param name="zoom">How close the new picture crops around the face (1 to 2, already checked); null = the default 1.</param>
    Task<Result<bool>> SetEmployeePhotoAsync(string employeeId, Stream content, CancellationToken ct, bool onAccount = false, double? zoom = null);
    Task<Result<bool>> ClearEmployeePhotoAsync(string employeeId, CancellationToken ct, bool onAccount = false);

    /// <summary>
    /// Changes how close the caller's current picture crops around the face (1 to 2, already checked), on the barber
    /// profile or, without one, on the account. Conflict when there is no picture to adjust.
    /// </summary>
    Task<Result<bool>> SetOwnPhotoZoomAsync(string userId, double zoom, CancellationToken ct);
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

    public const string ZoomMessage = "Zoom mora biti između 1 i 2.";

    /// <summary>
    /// Reads the optional zoom sent as a form field next to an uploaded picture. Always the invariant format
    /// ("1.5"), whatever the server's culture. Blank means "not given" (null, valid); anything that is not a number
    /// from 1 to 2 is invalid.
    /// </summary>
    public static bool TryParseZoom(string? raw, out double? zoom)
    {
        zoom = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (!double.TryParse(raw.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value) || !ValidZoom(value))
            return false;
        zoom = value;
        return true;
    }

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

    public const string CorruptMessage = "Slika nije ispravna.";

    /// <summary>
    /// The whole file checked: <see cref="Check"/> on its first bytes, then <see cref="IsIntact"/> on all of it, so a
    /// file that only starts like a picture (a header, a cut-off download) is refused here, not by the image host.
    /// </summary>
    public static string? CheckFile(ReadOnlySpan<byte> data, long maxBytes) =>
        Check(data.Length, data[..Math.Min(data.Length, 16)], maxBytes) ?? (IsIntact(data) ? null : CorruptMessage);

    /// <summary>
    /// Walks the container structure of a JPEG, PNG or WebP file without decoding the pixels: the picture has a size,
    /// every block fits inside the file, and the file is not cut short. False for anything else.
    /// </summary>
    public static bool IsIntact(ReadOnlySpan<byte> data) => Detect(data[..Math.Min(data.Length, 16)]) switch
    {
        "image/png" => PngIntact(data),
        "image/jpeg" => JpegIntact(data),
        "image/webp" => WebpIntact(data),
        _ => false,
    };

    private static uint BigEndian32(ReadOnlySpan<byte> b) => (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);

    private static uint LittleEndian32(ReadOnlySpan<byte> b) => (uint)(b[0] | b[1] << 8 | b[2] << 16 | b[3] << 24);

    /// <summary>IHDR first with a non-zero size, every chunk complete with a correct CRC, image data, and IEND.</summary>
    private static bool PngIntact(ReadOnlySpan<byte> data)
    {
        long offset = 8;
        var first = true;
        var sawData = false;
        while (offset + 12 <= data.Length)
        {
            var length = BigEndian32(data.Slice((int)offset, 4));
            if (length > int.MaxValue || offset + 12 + length > data.Length) return false;
            var type = data.Slice((int)offset + 4, 4);
            var body = data.Slice((int)offset + 8, (int)length);
            var crc = BigEndian32(data.Slice((int)(offset + 8 + length), 4));
            if (Crc32(data.Slice((int)offset + 4, 4 + (int)length)) != crc) return false;

            if (first)
            {
                if (!type.SequenceEqual("IHDR"u8) || length != 13) return false;
                if (BigEndian32(body) == 0 || BigEndian32(body[4..]) == 0) return false;
                first = false;
            }
            else if (type.SequenceEqual("IDAT"u8)) sawData = true;
            else if (type.SequenceEqual("IEND"u8)) return sawData;
            offset += 12 + length;
        }
        return false;
    }

    /// <summary>Segments up to the scan are complete, a frame header gives a size, and the scan is closed by EOI.</summary>
    private static bool JpegIntact(ReadOnlySpan<byte> data)
    {
        var offset = 2;
        var sawFrame = false;
        while (offset + 4 <= data.Length)
        {
            if (data[offset] != 0xFF) return false;
            var marker = data[offset + 1];
            if (marker == 0xFF) { offset++; continue; } // fill byte
            if (marker is 0x01 or (>= 0xD0 and <= 0xD7)) { offset += 2; continue; } // markers without a length
            if (marker == 0xD9) return false; // EOI before any scan

            var length = data[offset + 2] << 8 | data[offset + 3];
            if (length < 2 || offset + 2 + length > data.Length) return false;
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                if (length < 8) return false;
                var height = data[offset + 5] << 8 | data[offset + 6];
                var width = data[offset + 7] << 8 | data[offset + 8];
                if (width == 0 || height == 0) return false;
                sawFrame = true;
            }
            if (marker == 0xDA)
            {
                // The entropy-coded data follows; the picture must end with EOI after it (trailing bytes are tolerated).
                var scan = data[(offset + 2 + length)..];
                return sawFrame && scan.IndexOf(EndOfImage.AsSpan()) >= 0;
            }
            offset += 2 + length;
        }
        return false;
    }

    /// <summary>The RIFF size fits the file, every chunk fits the RIFF, and an image chunk with a non-zero size is there.</summary>
    private static bool WebpIntact(ReadOnlySpan<byte> data)
    {
        long riffEnd = 8L + LittleEndian32(data[4..]);
        if (riffEnd > data.Length || riffEnd < 20) return false;
        long offset = 12;
        var sawImage = false;
        while (offset + 8 <= riffEnd)
        {
            var type = data.Slice((int)offset, 4);
            long size = LittleEndian32(data[((int)offset + 4)..]);
            if (offset + 8 + size > riffEnd) return false;
            var body = data.Slice((int)offset + 8, (int)size);
            if (type.SequenceEqual("VP8 "u8))
            {
                // Frame tag (3 bytes), start code 9D 01 2A, then 14-bit width and height.
                if (size < 10 || body[3] != 0x9D || body[4] != 0x01 || body[5] != 0x2A) return false;
                if ((body[6] | body[7] << 8) % 0x4000 == 0 || (body[8] | body[9] << 8) % 0x4000 == 0) return false;
                sawImage = true;
            }
            else if (type.SequenceEqual("VP8L"u8))
            {
                if (size < 5 || body[0] != 0x2F) return false;
                sawImage = true; // width and height are stored minus one, so they are never zero
            }
            else if (type.SequenceEqual("ANMF"u8))
            {
                sawImage = true; // an animation frame carries its own image data
            }
            offset += 8 + size + (size & 1); // chunks are padded to an even size
        }
        return sawImage;
    }

    private static readonly byte[] EndOfImage = [0xFF, 0xD9];

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in bytes) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}

/// <summary>The image host refused the file as not a picture (it could not decode it).</summary>
public sealed class ImageRejectedException(string message) : InvalidRequestException(message);
