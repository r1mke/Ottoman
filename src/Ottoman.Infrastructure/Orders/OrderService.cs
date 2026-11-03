using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ottoman.Application.Common;
using Ottoman.Application.Orders;
using Ottoman.Application.Payments;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Orders;
using Ottoman.Domain.Reservations;
using Ottoman.Infrastructure.Payments;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Orders;

public class OrderService(
    OttomanDbContext db,
    IPaymentGateway gateway,
    PendingRefunds refunds,
    IOutbox outbox,
    IOptions<PaymentOptions> paymentOptions,
    TimeProvider clock,
    ILogger<OrderService> log) : IOrderService
{
    private const int MaxPendingOrdersPerClient = 5;
    private PaymentOptions Pay => paymentOptions.Value;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- create

    public async Task<Result<OrderDto>> CreateAsync(Actor actor, CreateOrderRequest r, CancellationToken ct)
    {
        if (!actor.IsClient) return Result<OrderDto>.Invalid("Narudžbu može kreirati samo klijent.");

        var requested = r.Items.Select(i => (i.ProductId, i.Quantity)).ToList();
        var error = OrderPricing.Validate(requested);
        if (error is not null) return Result<OrderDto>.Invalid(error);
        var lines = OrderPricing.Merge(requested);
        var ids = lines.Select(l => l.ProductId).ToList();

        // Prices come from the catalogue, never from the request.
        var products = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsActive)
            .Select(p => new { p.Id, p.Name, Price = p.SalePrice ?? p.Price, p.LowStockThreshold })
            .ToListAsync(ct);
        if (products.Count != ids.Count) return Result<OrderDto>.Invalid("Neki od proizvoda više nije dostupan.");

        // Unpaid card orders hold stock; cap them so one account cannot hoard the shelf.
        var pending = await db.Orders.CountAsync(o => o.ClientId == actor.UserId && o.Status == OrderStatus.PendingPayment, ct);
        if (r.PaymentMethod == PaymentMethod.Card && pending >= MaxPendingOrdersPerClient)
            return Result<OrderDto>.Invalid("Imate previše neplaćenih narudžbi. Platite ili otkažite neku od njih.");

        var now = Now;
        var order = new Order
        {
            ClientId = actor.UserId,
            Status = r.PaymentMethod == PaymentMethod.Card ? OrderStatus.PendingPayment : OrderStatus.Placed,
            Fulfilment = r.Fulfilment,
            PaymentMethod = r.PaymentMethod,
            DeliveryAddress = r.Fulfilment == FulfilmentType.Delivery ? r.DeliveryAddress?.Trim() : null,
            Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim(),
            CreatedAtUtc = now,
            ExpiresUtc = r.PaymentMethod == PaymentMethod.Card ? now.AddMinutes(Pay.PendingOrderMinutes) : null,
        };
        foreach (var line in lines)
        {
            var p = products.Single(x => x.Id == line.ProductId);
            order.Items.Add(new OrderItem { ProductId = p.Id, ProductName = p.Name, UnitPrice = p.Price, Quantity = line.Quantity });
        }
        order.TotalPrice = OrderPricing.Total(order.Items.Select(i => new OrderLine(i.ProductId, i.UnitPrice, i.Quantity)));

        string? shortOf = null;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            shortOf = null;
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                foreach (var item in order.Items)
                {
                    var seenPrice = item.UnitPrice;
                    var qty = item.Quantity;
                    var productId = item.ProductId;
                    // Atomic conditional update: succeeds only if enough stock is left AND the price is still the one
                    // the customer was quoted. No read-then-write, so concurrent buyers of the last piece cannot both win.
                    var rows = await db.Products
                        .Where(p => p.Id == productId && p.IsActive && p.Stock >= qty && (p.SalePrice ?? p.Price) == seenPrice)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - qty), ct);
                    if (rows == 0)
                    {
                        shortOf = item.ProductName;
                        await tx.RollbackAsync(ct);
                        return;
                    }
                }

                foreach (var p in products.Where(p => p.LowStockThreshold != null))
                {
                    var stock = await db.Products.Where(x => x.Id == p.Id).Select(x => x.Stock).SingleAsync(ct);
                    if (stock <= p.LowStockThreshold) outbox.Enqueue(new ProductLowStock(p.Id, p.Name, stock, p.LowStockThreshold!.Value));
                }

                db.Orders.Add(order);
                await db.SaveChangesAsync(ct); // assigns the id used by the event below
                if (order.Status == OrderStatus.Placed)
                {
                    outbox.Enqueue(new OrderPlaced(order.Id, order.ClientId, order.TotalPrice, order.PaymentMethod.ToString()));
                    await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        if (shortOf is not null)
            return Result<OrderDto>.Conflict($"„{shortOf}“ nema dovoljno na zalihi ili mu se cijena promijenila. Osvježite košaricu.");

        string? secret = null;
        if (order.PaymentMethod == PaymentMethod.Card)
        {
            try
            {
                secret = await AttachPaymentAsync(order, ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Could not create a payment for order {OrderId}; closing it", order.Id);
                await CloseAsync(order.Id, OrderStatus.PendingPayment, OrderStatus.Cancelled, "System", ct);
                return Result<OrderDto>.Invalid("Plaćanje karticom trenutno nije dostupno. Pokušajte ponovo kasnije.");
            }
        }

        var dto = await LoadDtoAsync(actor, order.Id, ct);
        return Result<OrderDto>.Ok(dto! with { PaymentClientSecret = secret });
    }

    private async Task<string> AttachPaymentAsync(Order order, CancellationToken ct)
    {
        var amountMinor = OrderPricing.ToMinorUnits(order.TotalPrice, Pay.BamPerEur);
        var intent = await gateway.CreateIntentAsync(order.Id, amountMinor, Pay.Currency, ct);

        var payment = new Payment
        {
            ProviderRef = intent.ProviderRef,
            Amount = amountMinor / 100m,
            Currency = Pay.Currency.ToUpperInvariant(),
            Status = PaymentStatus.Pending,
            ClientSecret = intent.ClientSecret,
            CreatedAtUtc = Now,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);

        await db.Orders.Where(o => o.Id == order.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentId, payment.Id), ct);
        return intent.ClientSecret;
    }

    // ---------------------------------------------------------------- read

    public async Task<PagedResult<OrderDto>> ListAsync(Actor actor, OrderFilter filter, PageQuery page, CancellationToken ct)
    {
        if (actor.IsEmployee) return new PagedResult<OrderDto>([], 0);

        var query = db.Orders.AsNoTracking().AsQueryable();
        if (actor.IsClient) query = query.Where(o => o.ClientId == actor.UserId);
        else if (!string.IsNullOrWhiteSpace(filter.ClientId)) query = query.Where(o => o.ClientId == filter.ClientId);
        if (filter.Status is { } s) query = query.Where(o => o.Status == s);

        var total = await query.CountAsync(ct);
        var orders = await query.Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc).ThenByDescending(o => o.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(await ToDtosAsync(orders, includeSecret: false, actor, ct), total);
    }

    public async Task<Result<OrderDto>> GetAsync(Actor actor, int id, CancellationToken ct)
    {
        var dto = await LoadDtoAsync(actor, id, ct);
        return dto is null ? Result<OrderDto>.NotFound("Narudžba nije pronađena.") : Result<OrderDto>.Ok(dto);
    }

    private async Task<OrderDto?> LoadDtoAsync(Actor actor, int id, CancellationToken ct)
    {
        if (actor.IsEmployee) return null;
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);
        // Someone else's order looks exactly like one that does not exist.
        if (order is null || (actor.IsClient && order.ClientId != actor.UserId)) return null;
        return (await ToDtosAsync([order], includeSecret: actor.IsClient, actor, ct)).Single();
    }

    private async Task<IReadOnlyList<OrderDto>> ToDtosAsync(List<Order> orders, bool includeSecret, Actor actor, CancellationToken ct)
    {
        var paymentIds = orders.Where(o => o.PaymentId != null).Select(o => o.PaymentId!.Value).Distinct().ToList();
        var payments = await db.Payments.AsNoTracking().Where(p => paymentIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Status, p.ClientSecret }).ToDictionaryAsync(p => p.Id, ct);

        var clientIds = orders.Select(o => o.ClientId).Distinct().ToList();
        var names = actor.IsAdmin
            ? await db.Users.AsNoTracking().Where(u => clientIds.Contains(u.Id))
                .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() }).ToDictionaryAsync(u => u.Id, u => u.Name, ct)
            : [];

        return orders.Select(o =>
        {
            payments.TryGetValue(o.PaymentId ?? 0, out var pay);
            var secret = includeSecret && o.Status == OrderStatus.PendingPayment ? pay?.ClientSecret : null;
            return new OrderDto(
                o.Id, o.ClientId, names.GetValueOrDefault(o.ClientId),
                o.Status, o.Fulfilment, o.PaymentMethod, pay?.Status,
                o.DeliveryAddress, o.Note, o.TotalPrice,
                o.CreatedAtUtc, o.ExpiresUtc, o.PaidAtUtc, o.FulfilledAtUtc, o.CancelledAtUtc, o.CancelledBy,
                o.Items.OrderBy(i => i.Id).Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity)).ToList(),
                secret);
        }).ToList();
    }

    // ---------------------------------------------------------------- change

    public async Task<Result<OrderDto>> CancelAsync(Actor actor, int id, CancellationToken ct)
    {
        if (actor.IsEmployee) return Result<OrderDto>.NotFound("Narudžba nije pronađena.");
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null || (actor.IsClient && order.ClientId != actor.UserId))
            return Result<OrderDto>.NotFound("Narudžba nije pronađena.");

        var verdict = OrderStateMachine.CanCancel(order.Status, actor.Kind, order.PaymentMethod);
        if (!verdict.Allowed) return Result<OrderDto>.Invalid(verdict.Reason!);

        var by = actor.IsAdmin ? "Admin" : "Client";
        if (!await CloseAsync(id, order.Status, OrderStatus.Cancelled, by, ct))
            return Result<OrderDto>.Conflict("Narudžba je u međuvremenu promijenjena. Osvježite prikaz.");

        return Result<OrderDto>.Ok((await LoadDtoAsync(actor, id, ct))!);
    }

    public async Task<Result<OrderDto>> FulfilAsync(Actor actor, int id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null || !actor.IsAdmin) return Result<OrderDto>.NotFound("Narudžba nije pronađena.");

        var verdict = OrderStateMachine.CanFulfil(order.Status, actor.Kind);
        if (!verdict.Allowed) return Result<OrderDto>.Invalid(verdict.Reason!);

        var now = Now;
        var rows = await db.Orders.Where(o => o.Id == id && o.Status == OrderStatus.Placed)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Fulfilled).SetProperty(o => o.FulfilledAtUtc, now), ct);
        if (rows == 0) return Result<OrderDto>.Conflict("Narudžba je u međuvremenu promijenjena. Osvježite prikaz.");

        // Cash/at-pickup orders are settled at handover; nothing to record for card orders (already paid).
        return Result<OrderDto>.Ok((await LoadDtoAsync(actor, id, ct))!);
    }

    public async Task<int> ExpireUnpaidAsync(CancellationToken ct)
    {
        var now = Now;
        var due = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.PendingPayment && o.ExpiresUtc != null && o.ExpiresUtc < now)
            .OrderBy(o => o.ExpiresUtc).Select(o => o.Id).Take(100).ToListAsync(ct);

        var closed = 0;
        foreach (var id in due)
            if (await CloseAsync(id, OrderStatus.PendingPayment, OrderStatus.Expired, "System", ct)) closed++;
        return closed;
    }

    /// <summary>
    /// Moves an order from <paramref name="expected"/> to a closed status and returns its stock to the shelf, in
    /// one transaction. The status change is conditional, so two concurrent closers cannot both return the stock.
    /// Money is handled afterwards, outside the transaction.
    /// </summary>
    private async Task<bool> CloseAsync(int orderId, OrderStatus expected, OrderStatus closed, string by, CancellationToken ct)
    {
        var now = Now;
        var done = false;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            done = false;
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var rows = await db.Orders.Where(o => o.Id == orderId && o.Status == expected)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(o => o.Status, closed)
                        .SetProperty(o => o.CancelledAtUtc, now)
                        .SetProperty(o => o.CancelledBy, by), ct);
                if (rows == 0)
                {
                    await tx.RollbackAsync(ct);
                    return;
                }

                var items = await db.OrderItems.Where(i => i.OrderId == orderId)
                    .Select(i => new { i.ProductId, i.Quantity }).ToListAsync(ct);
                foreach (var item in items)
                {
                    var q = item.Quantity;
                    await db.Products.Where(p => p.Id == item.ProductId)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + q), ct);
                }

                var closedOrder = await db.Orders.Where(o => o.Id == orderId).Select(o => new { o.ClientId, o.PaymentId }).SingleAsync(ct);
                // A paid order that closes owes a refund. Recording that here, with the close, means a refund call
                // that fails afterwards is retried by the expiry job instead of being forgotten.
                if (closedOrder.PaymentId is { } paymentId)
                    await db.Payments.Where(p => p.Id == paymentId && p.Status == PaymentStatus.Succeeded)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.RefundPending), ct);
                outbox.Enqueue(new OrderCancelled(orderId, closedOrder.ClientId, by));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                done = true;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        if (done) await SettlePaymentAfterCloseAsync(orderId, ct);
        return done;
    }

    private async Task SettlePaymentAfterCloseAsync(int orderId, CancellationToken ct)
    {
        var paymentId = await db.Orders.AsNoTracking().Where(o => o.Id == orderId).Select(o => o.PaymentId).SingleAsync(ct);
        if (paymentId is null) return;
        var payment = await db.Payments.AsNoTracking().Where(p => p.Id == paymentId)
            .Select(p => new { p.Id, p.ProviderRef, p.Status }).SingleAsync(ct);

        if (payment.Status == PaymentStatus.RefundPending)
        {
            // A failure leaves the payment visibly "refund pending"; RetryPendingRefundsAsync finishes it later.
            // Stripe's idempotency key makes a repeated attempt safe.
            await refunds.CompleteAsync(payment.Id, payment.ProviderRef, ct);
            return;
        }

        try
        {
            if (payment.Status == PaymentStatus.Pending)
            {
                await db.Payments.Where(p => p.Id == payment.Id && p.Status == PaymentStatus.Pending)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Failed), ct);
                await gateway.CancelIntentAsync(payment.ProviderRef, ct);
            }
        }
        catch (Exception ex)
        {
            // The order is already closed and its stock returned; an intent left open simply never completes.
            log.LogError(ex, "Payment settlement after closing order {OrderId} failed (payment {PaymentId})", orderId, payment.Id);
        }
    }

    public Task<int> RetryPendingRefundsAsync(CancellationToken ct) => refunds.RetryAllAsync(ct);
}
