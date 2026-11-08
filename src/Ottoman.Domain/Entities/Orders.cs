namespace Ottoman.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public string ClientId { get; set; } = null!;
    public OrderStatus Status { get; set; }
    public FulfilmentType Fulfilment { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Note { get; set; }

    // Snapshot: computed by the server from current prices at the moment of ordering.
    public decimal TotalPrice { get; set; }

    public int? PaymentId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Unpaid card orders hold stock only until this moment.</summary>
    public DateTime? ExpiresUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? FulfilledAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelledBy { get; set; }
    /// <summary>Why the salon cancelled (required for an admin); the client sees it. Null for client cancels and expiry.</summary>
    public string? CancelReason { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
