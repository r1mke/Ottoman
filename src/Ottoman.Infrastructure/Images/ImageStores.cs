using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ottoman.Application.Images;

namespace Ottoman.Infrastructure.Images;

/// <summary>
/// Cloudinary over its REST API. Requests are signed with the API secret (SHA-1 of the sorted parameters plus the
/// secret), so the apps never hold a credential that could write to the account.
/// </summary>
public sealed class CloudinaryImageStore(HttpClient http, IOptions<ImageOptions> options, TimeProvider clock) : IImageStore
{
    public static readonly Uri BaseAddress = new("https://api.cloudinary.com/v1_1/");

    public async Task<string> UploadAsync(Stream content, string folder, CancellationToken ct)
    {
        var o = options.Value;
        var timestamp = clock.GetUtcNow().ToUnixTimeSeconds().ToString();
        using var form = new MultipartFormDataContent
        {
            { new StreamContent(content), "file", "upload" },
            { new StringContent(o.ApiKey), "api_key" },
            { new StringContent(folder), "folder" },
            { new StringContent(timestamp), "timestamp" },
            { new StringContent(Sign(o.ApiSecret, ("folder", folder), ("timestamp", timestamp))), "signature" },
        };

        using var response = await http.PostAsync($"{o.CloudName}/image/upload", form, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        // 400 is Cloudinary's answer to a file it cannot read as a picture ("Invalid image file"); the caller turns it
        // into a 400 for the user. Anything else (auth, rate limit, outage) is our problem and stays a server error.
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest) throw new ImageRejectedException(ImageRules.CorruptMessage);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Cloudinary upload failed ({(int)response.StatusCode}).");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("public_id").GetString()
               ?? throw new InvalidOperationException("Cloudinary returned no public_id.");
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        var o = options.Value;
        var timestamp = clock.GetUtcNow().ToUnixTimeSeconds().ToString();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["public_id"] = key,
            ["invalidate"] = "true",
            ["api_key"] = o.ApiKey,
            ["timestamp"] = timestamp,
            ["signature"] = Sign(o.ApiSecret, ("invalidate", "true"), ("public_id", key), ("timestamp", timestamp)),
        });
        using var response = await http.PostAsync($"{o.CloudName}/image/destroy", form, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Parameters sorted by name, joined as a=b&amp;c=d, secret appended, SHA-1 as hex.</summary>
    public static string Sign(string secret, params (string Name, string Value)[] parameters)
    {
        var text = string.Join("&", parameters.OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => $"{p.Name}={p.Value}")) + secret;
        return Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(text)));
    }
}

/// <summary>Used when no Cloudinary account is configured: nothing leaves the machine, uploads just get a key.</summary>
public sealed class OfflineImageStore : IImageStore
{
    public List<string> Uploaded { get; } = [];
    public List<string> Deleted { get; } = [];

    public Task<string> UploadAsync(Stream content, string folder, CancellationToken ct)
    {
        var key = $"{folder}/fake-{Guid.NewGuid():N}";
        lock (Uploaded) Uploaded.Add(key);
        return Task.FromResult(key);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        lock (Deleted) Deleted.Add(key);
        return Task.CompletedTask;
    }
}

/// <summary>Builds the delivery URL for a stored key: capped width, automatic format and quality.</summary>
public class ImageUrlBuilder(IOptions<ImageOptions> options)
{
    public string? Url(string? key) => ImageRules.DeliveryUrl(options.Value.CloudName, key);

    public string? AvatarUrl(string? key, double zoom) => ImageRules.AvatarUrl(options.Value.CloudName, key, zoom);
}
