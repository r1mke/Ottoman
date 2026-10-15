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
    string? DeliveryAddress = null,
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
        RuleFor(x => x.Fulfilment).IsInEnum();
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.DeliveryAddress).NotEmpty().MaximumLength(300)
            .When(x => x.Fulfilment == FulfilmentType.Delivery)
            .WithMessage("Unesite adresu za dostavu.").WithName("Adresa");
        RuleFor(x => x.Note).MaximumLength(500).WithName("Napomena");
        // Paying on receipt means paying the person who hands over the goods; a courier flow is out of scope for now.
        RuleFor(x => x.PaymentMethod).Equal(PaymentMethod.Card)
            .When(x => x.Fulfilment == FulfilmentType.Delivery)
            .WithMessage("Dostava se plaća karticom.");
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
    string? DeliveryAddress,
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
    string? PaymentClientSecret = null);

public record OrderFilter(OrderStatus? Status = null, string? ClientId = null);

public interface IOrderService
{
    Task<Result<OrderDto>> CreateAsync(Actor actor, CreateOrderRequest request, CancellationToken ct);
    Task<PagedResult<OrderDto>> ListAsync(Actor actor, OrderFilter filter, PageQuery page, CancellationToken ct);
    Task<Result<OrderDto>> GetAsync(Actor actor, int id, CancellationToken ct);
    Task<Result<OrderDto>> CancelAsync(Actor actor, int id, CancellationToken ct);
    Task<Result<OrderDto>> FulfilAsync(Actor actor, int id, CancellationToken ct);

    /// <summary>Releases stock of card orders that were not paid in time. Returns how many were closed.</summary>
    Task<int> ExpireUnpaidAsync(CancellationToken ct);
}
