using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ottoman.Application.Payments;

namespace Ottoman.Infrastructure.Payments;

/// <summary>Talks to the Stripe REST API directly (three calls; no SDK needed).</summary>
public class StripeGateway(HttpClient http, IOptions<PaymentOptions> options) : IPaymentGateway
{
    public static readonly Uri BaseAddress = new("https://api.stripe.com/");

    public async Task<PaymentIntentResult> CreateIntentAsync(int orderId, long amountMinor, string currency, CancellationToken ct)
    {
        using var request = Form(HttpMethod.Post, "v1/payment_intents", new Dictionary<string, string>
        {
            ["amount"] = amountMinor.ToString(),
            ["currency"] = currency,
            ["automatic_payment_methods[enabled]"] = "true",
            ["metadata[orderId]"] = orderId.ToString(),
        });
        // Retrying the same order never creates a second charge.
        request.Headers.Add("Idempotency-Key", $"ottoman-order-{orderId}");

        using var doc = await SendAsync(request, ct);
        var root = doc.RootElement;
        return new PaymentIntentResult(root.GetProperty("id").GetString()!, root.GetProperty("client_secret").GetString()!);
    }

    public async Task RefundAsync(string providerRef, CancellationToken ct)
    {
        using var request = Form(HttpMethod.Post, "v1/refunds", new Dictionary<string, string> { ["payment_intent"] = providerRef });
        request.Headers.Add("Idempotency-Key", $"ottoman-refund-{providerRef}");
        using var _ = await SendAsync(request, ct);
    }

    public async Task CancelIntentAsync(string providerRef, CancellationToken ct)
    {
        using var request = Form(HttpMethod.Post, $"v1/payment_intents/{Uri.EscapeDataString(providerRef)}/cancel", []);
        using var _ = await SendAsync(request, ct);
    }

    private HttpRequestMessage Form(HttpMethod method, string path, Dictionary<string, string> fields)
    {
        var request = new HttpRequestMessage(method, path) { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.SecretKey);
        return request;
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Stripe returned {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }
}

/// <summary>
/// Used when no Stripe key is configured (local development, tests). It never moves money and never
/// reports a payment on its own: an order only becomes paid through a correctly signed webhook.
/// </summary>
public class OfflinePaymentGateway : IPaymentGateway
{
    public ConcurrentQueue<string> Refunds { get; } = new();
    public ConcurrentQueue<string> Cancelled { get; } = new();
    public ConcurrentQueue<(int OrderId, long AmountMinor, string Currency)> Created { get; } = new();

    public Task<PaymentIntentResult> CreateIntentAsync(int orderId, long amountMinor, string currency, CancellationToken ct)
    {
        Created.Enqueue((orderId, amountMinor, currency));
        var id = $"pi_fake_{Guid.NewGuid():N}";
        return Task.FromResult(new PaymentIntentResult(id, $"{id}_secret_fake"));
    }

    public Task RefundAsync(string providerRef, CancellationToken ct)
    {
        Refunds.Enqueue(providerRef);
        return Task.CompletedTask;
    }

    public Task CancelIntentAsync(string providerRef, CancellationToken ct)
    {
        Cancelled.Enqueue(providerRef);
        return Task.CompletedTask;
    }
}
