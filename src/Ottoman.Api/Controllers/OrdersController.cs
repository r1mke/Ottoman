using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ottoman.Api.Auth;
using Ottoman.Api.Filters;
using Ottoman.Application.Common;
using Ottoman.Application.Orders;
using Ottoman.Application.Payments;
using Ottoman.Domain;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize(Roles = $"{Roles.Client},{Roles.Admin}")]
public class OrdersController(IOrderService orders) : ControllerBase
{
    /// <summary>
    /// Places an order. The body names products and quantities only; prices, totals and the payment amount
    /// are computed here. A card order returns a client secret to complete the payment.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Client)]
    [EnableRateLimiting(Policies.OrderLimit)]
    [Idempotent]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var result = await orders.CreateAsync(User.ToActor(), request, ct);
        return result.Succeeded
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : this.ToActionResult(result);
    }

    /// <summary>Clients: their own orders. Admins: all, optionally by status or client.</summary>
    [HttpGet]
    public Task<PagedResult<OrderDto>> List(
        [FromQuery] OrderStatus? status,
        [FromQuery] string? clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        orders.ListAsync(User.ToActor(), new OrderFilter(status, clientId), new PageQuery(page, pageSize), ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> Get(int id, CancellationToken ct) =>
        this.ToActionResult(await orders.GetAsync(User.ToActor(), id, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(int id, CancellationToken ct) =>
        this.ToActionResult(await orders.CancelAsync(User.ToActor(), id, ct));

    [HttpPost("{id:int}/fulfil")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<OrderDto>> Fulfil(int id, CancellationToken ct) =>
        this.ToActionResult(await orders.FulfilAsync(User.ToActor(), id, ct));
}

public record PaymentConfigDto(string? PublishableKey, bool CardsEnabled);

[ApiController]
[Route("api/v1/payments")]
public class PaymentsController(IPaymentWebhookHandler webhook, Microsoft.Extensions.Options.IOptions<PaymentOptions> options) : ControllerBase
{
    /// <summary>What the apps need to offer card payment. Cards are enabled only when both Stripe keys are configured.</summary>
    [HttpGet("config")]
    [Authorize]
    public PaymentConfigDto Config() =>
        new(
            string.IsNullOrWhiteSpace(options.Value.PublishableKey) ? null : options.Value.PublishableKey,
            !string.IsNullOrWhiteSpace(options.Value.PublishableKey) && !string.IsNullOrWhiteSpace(options.Value.SecretKey));

    /// <summary>
    /// Stripe calls this. Anonymous on purpose: the only credential is the signature over the raw body,
    /// so the body is read as-is and nothing is acted on before the signature is verified.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(ct);
        var outcome = await webhook.HandleAsync(payload, Request.Headers["Stripe-Signature"].FirstOrDefault(), ct);
        return outcome switch
        {
            WebhookOutcome.Processed => Ok(),
            WebhookOutcome.Unauthorized => Unauthorized(),
            _ => BadRequest(),
        };
    }
}
