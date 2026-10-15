using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ottoman.Application.Payments;

/// <summary>
/// Verifies Stripe's <c>Stripe-Signature</c> header (HMAC-SHA256 over "timestamp.payload", with a replay
/// window) and turns the JSON body into a <see cref="PaymentEvent"/>. Pure: no I/O, easy to test.
/// </summary>
public static class StripeWebhook
{
    public const int ToleranceSeconds = 300;

    public static bool VerifySignature(string payload, string? header, string secret, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(header) || string.IsNullOrEmpty(secret)) return false;

        long? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "t" && long.TryParse(kv[1], out var t)) timestamp = t;
            else if (kv[0] == "v1") signatures.Add(kv[1]);
        }
        if (timestamp is null || signatures.Count == 0) return false;

        var age = Math.Abs(now.ToUnixTimeSeconds() - timestamp.Value);
        if (age > ToleranceSeconds) return false;

        var expected = Sign(payload, timestamp.Value, secret);
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        // Compare every candidate in constant time; any match is enough (Stripe sends several while rotating secrets).
        return signatures.Aggregate(false, (ok, s) =>
            CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(s), expectedBytes) | ok);
    }

    public static string Sign(string payload, long timestamp, string secret)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    /// <summary>Builds a header a real Stripe would send. Used by tests and local tooling.</summary>
    public static string Header(string payload, string secret, DateTimeOffset now) =>
        $"t={now.ToUnixTimeSeconds()},v1={Sign(payload, now.ToUnixTimeSeconds(), secret)}";

    public static PaymentEvent? Parse(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var id = root.GetProperty("id").GetString() ?? "";
            var type = root.GetProperty("type").GetString();
            var kind = type switch
            {
                "payment_intent.succeeded" => PaymentEventKind.Succeeded,
                "payment_intent.payment_failed" => PaymentEventKind.Failed,
                _ => PaymentEventKind.Ignored,
            };
            if (kind == PaymentEventKind.Ignored) return new PaymentEvent(id, kind, "", 0, "");

            var obj = root.GetProperty("data").GetProperty("object");
            var amount = obj.TryGetProperty("amount_received", out var received) && received.ValueKind == JsonValueKind.Number && kind == PaymentEventKind.Succeeded
                ? received.GetInt64()
                : obj.GetProperty("amount").GetInt64();
            return new PaymentEvent(
                id, kind,
                obj.GetProperty("id").GetString() ?? "",
                amount,
                (obj.GetProperty("currency").GetString() ?? "").ToLowerInvariant());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
