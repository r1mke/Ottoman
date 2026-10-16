using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ottoman.Application.Push;

namespace Ottoman.Infrastructure.Push;

/// <summary>
/// Firebase Cloud Messaging over the HTTP v1 API. Authenticates as the service account: a short-lived RS256 JWT
/// is exchanged for an OAuth access token, which is cached until shortly before it expires.
/// Neither the private key nor device tokens are ever logged.
/// </summary>
public sealed class FcmPushSender : IPushSender, IDisposable
{
    public const string ClientName = "fcm";
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly IHttpClientFactory _http;
    private readonly ILogger<FcmPushSender> _log;
    private readonly TimeProvider _clock;
    private readonly bool _validateOnly;
    private readonly string _projectId;
    private readonly string _clientEmail;
    private readonly string _tokenUri;
    private readonly RSA _key = RSA.Create();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpires;

    public FcmPushSender(IHttpClientFactory http, IOptions<PushOptions> options, TimeProvider clock, ILogger<FcmPushSender> log)
    {
        _http = http;
        _clock = clock;
        _log = log;
        var o = options.Value;
        _validateOnly = o.ValidateOnly;

        var json = o.ServiceAccountJson != "" ? o.ServiceAccountJson : File.ReadAllText(o.ServiceAccountPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        _projectId = Required(root, "project_id");
        _clientEmail = Required(root, "client_email");
        _tokenUri = root.TryGetProperty("token_uri", out var uri) && uri.GetString() is { Length: > 0 } u
            ? u
            : "https://oauth2.googleapis.com/token";
        _key.ImportFromPem(Required(root, "private_key"));
    }

    private static string Required(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.GetString() is { Length: > 0 } s
            ? s
            : throw new InvalidOperationException($"The Firebase service account key has no '{name}'.");

    public async Task<PushOutcome> SendAsync(string token, PushMessage message, CancellationToken ct)
    {
        try
        {
            var access = await AccessTokenAsync(ct);
            var body = JsonSerializer.Serialize(new
            {
                validate_only = _validateOnly ? true : (bool?)null,
                message = new
                {
                    token,
                    notification = new { title = message.Title, body = PushPolicy.Preview(message.Body) },
                    data = new Dictionary<string, string> { ["kind"] = message.Kind },
                    android = new { priority = "HIGH", notification = new { channel_id = "ottoman_default" } },
                },
            }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new("Bearer", access);

            using var response = await _http.CreateClient(ClientName).SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return PushOutcome.Sent;

            var text = await response.Content.ReadAsStringAsync(ct);
            var outcome = Classify((int)response.StatusCode, text);
            if (outcome == PushOutcome.Failed)
                _log.LogWarning("FCM refused a push ({Status}): {Detail}", (int)response.StatusCode, Truncate(text));
            return outcome;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _log.LogWarning(ex, "Push could not be sent");
            return PushOutcome.Failed;
        }
    }

    /// <summary>404 (UNREGISTERED) and a 400 about the token mean the token is dead; everything else is our or Google's problem.</summary>
    public static PushOutcome Classify(int status, string responseBody)
    {
        if (status == 404) return PushOutcome.InvalidToken;
        if (status == 400 && responseBody.Contains("registration token", StringComparison.OrdinalIgnoreCase)) return PushOutcome.InvalidToken;
        if (status == 400 && responseBody.Contains("\"field\": \"message.token\"", StringComparison.OrdinalIgnoreCase)) return PushOutcome.InvalidToken;
        if (status == 400 && responseBody.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)) return PushOutcome.InvalidToken;
        return PushOutcome.Failed;
    }

    private static string Truncate(string s) => s.Length <= 300 ? s : s[..300];

    private async Task<string> AccessTokenAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        if (_accessToken is not null && now < _accessTokenExpires - TimeSpan.FromMinutes(5)) return _accessToken;

        await _gate.WaitAsync(ct);
        try
        {
            now = _clock.GetUtcNow();
            if (_accessToken is not null && now < _accessTokenExpires - TimeSpan.FromMinutes(5)) return _accessToken;

            var assertion = BuildAssertion(now);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            });
            using var response = await _http.CreateClient(ClientName).PostAsync(_tokenUri, form, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Google refused the service account sign-in ({(int)response.StatusCode}).");

            using var doc = JsonDocument.Parse(text);
            _accessToken = doc.RootElement.GetProperty("access_token").GetString()
                           ?? throw new InvalidOperationException("Google returned no access token.");
            _accessTokenExpires = now.AddSeconds(doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600);
            return _accessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string BuildAssertion(DateTimeOffset now)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = _clientEmail,
            scope = Scope,
            aud = _tokenUri,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddMinutes(55).ToUnixTimeSeconds(),
        }));
        var signature = _key.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{claims}.{Base64Url(signature)}";
    }

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose()
    {
        _key.Dispose();
        _gate.Dispose();
    }
}

/// <summary>Used when no Firebase key is configured: pushes are recorded, nothing leaves the machine.</summary>
public sealed class FakePushSender : IPushSender
{
    private readonly List<(string Token, PushMessage Message)> _sent = [];

    /// <summary>Tokens the fake treats as dead, to exercise the cleanup path.</summary>
    public HashSet<string> DeadTokens { get; } = [];

    public IReadOnlyList<(string Token, PushMessage Message)> Sent
    {
        get
        {
            lock (_sent) return _sent.ToList();
        }
    }

    public Task<PushOutcome> SendAsync(string token, PushMessage message, CancellationToken ct)
    {
        if (DeadTokens.Contains(token)) return Task.FromResult(PushOutcome.InvalidToken);
        lock (_sent) _sent.Add((token, message));
        return Task.FromResult(PushOutcome.Sent);
    }
}
