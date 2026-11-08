using FluentValidation;
using Ottoman.Application.Common;
using Ottoman.Domain;

namespace Ottoman.Application.Orders;

/// <summary>The client says which products and how many; prices and totals are always computed on the server.</summary>
public record OrderLineRequest(int ProductId, int Quantity);

public record CreateOrderRequest(
    IReadOnlyList<OrderLineRequest> Items,
    FulfilmentType Fulfilment,
    PaymentMethod PaymentMethod,
    string? Note = null);

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.Items).NotNull().NotEmpty().WithMessage("Košarica je prazna.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(l => l.ProductId).GreaterThan(0);
            i.RuleFor(l => l.Quantity).InclusiveBetween(1, Ottoman.Domain.Orders.OrderPricing.MaxQuantityPerLine)
                .WithName("Količina");
        });
        // Goods are collected at the salon: Pickup is the only fulfilment there is.
        RuleFor(x => x.Fulfilment).IsInEnum().WithMessage("Proizvodi se preuzimaju u salonu.");
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500).WithName("Napomena");
    }
}

public record OrderItemDto(int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderDto(
    int Id,
    string ClientId,
    string? ClientName,
    OrderStatus Status,
    FulfilmentType Fulfilment,
    PaymentMethod PaymentMethod,
    PaymentStatus? PaymentStatus,
    string? Note,
    decimal TotalPrice,
    DateTime CreatedAtUtc,
    DateTime? ExpiresUtc,
    DateTime? PaidAtUtc,
    DateTime? FulfilledAtUtc,
    DateTime? CancelledAtUtc,
    string? CancelledBy,
    IReadOnlyList<OrderItemDto> Items,
    /// <summary>Present only on the owner's pending card orders: lets the app confirm the payment.</summary>
    string? PaymentClientSecret = null,
    /// <summary>The money is in (decided by the server: card payment succeeded, or paid at pickup).</summary>
    bool IsPaid = false,
    /// <summary>Why the salon cancelled the order; shown to the client.</summary>
    string? CancelReason = null);

/// <summary>The salon must say why it cancels an order (the client gets it); a client may cancel without a reason.</summary>
public record CancelOrderRequest(string? Reason);

public class CancelOrderRequestValidator : AbstractValidator<CancelOrderRequest>
{
    public CancelOrderRequestValidator() =>
        RuleFor(x => x.Reason).MaximumLength(Ottoman.Domain.Reservations.CancellationRules.MaxReasonLength).WithName("Razlog");
}

public record OrderFilter(OrderStatus? Status = null, string? ClientId = null);

public interface IOrderService
{
    Task<Result<OrderDto>> CreateAsync(Actor actor, CreateOrderRequest request, CancellationToken ct);
    Task<PagedResult<OrderDto>> ListAsync(Actor actor, OrderFilter filter, PageQuery page, CancellationToken ct);
    Task<Result<OrderDto>> GetAsync(Actor actor, int id, CancellationToken ct);
    Task<Result<OrderDto>> CancelAsync(Actor actor, int id, string? reason, CancellationToken ct);
    Task<Result<OrderDto>> FulfilAsync(Actor actor, int id, CancellationToken ct);

    /// <summary>
    /// The app reports that the card sheet finished. The server asks the processor itself and finalises the order
    /// through the same idempotent path as the webhook; nothing the app says is taken as proof of payment.
    /// </summary>
    Task<Result<OrderDto>> ConfirmPaymentAsync(Actor actor, int id, CancellationToken ct);

    /// <summary>Releases stock of card orders that were not paid in time. Returns how many were closed.</summary>
    Task<int> ExpireUnpaidAsync(CancellationToken ct);

    /// <summary>Retries refunds that were decided but did not go through at the processor. Returns how many did now.</summary>
    Task<int> RetryPendingRefundsAsync(CancellationToken ct);
}
