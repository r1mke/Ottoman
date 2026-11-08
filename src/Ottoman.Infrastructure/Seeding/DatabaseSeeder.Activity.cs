using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Reservations;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Identity;

namespace Ottoman.Infrastructure.Seeding;

/// <summary>
/// Demo history, so that every screen (dashboard, reports, reviews, recommendations, inbox) has something to show on
/// a fresh database: three months of appointments, orders, ratings and messages of fictional clients (their data is
/// invented, never copied from real accounts). Deterministic in shape: the same dice every time, dates relative to today.
/// </summary>
public partial class DatabaseSeeder
{
    private const int HistoryDays = 90;

    private static readonly (string First, string Last, string Email)[] DemoClients =
    [
        ("Elmir", "Mujkić", "elmir@ottoman.local"),
        ("Amra", "Hodžić", "amra@ottoman.local"),
        ("Haris", "Kovačević", "haris@ottoman.local"),
        ("Lejla", "Softić", "lejla@ottoman.local"),
        ("Emir", "Delić", "emir@ottoman.local"),
        ("Selma", "Begić", "selma@ottoman.local"),
        ("Adnan", "Ibrahimović", "adnan@ottoman.local"),
        ("Mirza", "Čaušević", "mirza@ottoman.local"),
    ];

    private static readonly string[] EmployeeComments =
    [
        "Odlično šišanje, tačno kako sam tražio.",
        "Vrlo profesionalan i ljubazan, preporuka.",
        "Brzo i uredno. Sigurno dolazim opet.",
        "Najbolji fade koji sam imao.",
        "Ugodna atmosfera i odličan rezultat.",
        "Dobro, ali sam čekao desetak minuta.",
        "Solidno, bez zamjerki.",
    ];

    private static readonly string[] ServiceComments =
    [
        "Usluga je vrijedna svake marke.",
        "Uređeno do detalja.",
        "Opuštajuće i temeljito.",
        "Cijena je u redu za ovaj kvalitet.",
        "Moglo je biti malo detaljnije.",
    ];

    private static readonly string[] ProductComments =
    [
        "Odlična fiksacija i miris.",
        "Dobar proizvod, koristim svaki dan.",
        "Kvalitetno, vrijedi cijenu.",
        "Fino se nanosi i lako ispire.",
        "Očekivao sam malo više.",
    ];

    private async Task SeedActivityAsync(
        IReadOnlyList<AppUser> employees,
        IReadOnlyList<AppUser> clients,
        IReadOnlyList<Service> services,
        IReadOnlyList<Product> products,
        CancellationToken ct)
    {
        var rnd = new Random(220048);
        var settings = await db.SalonSettings.AsNoTracking().SingleAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(LocalTime.ToLocal(now, zone));

        var offered = await db.EmployeeServices.AsNoTracking().ToListAsync(ct);
        var servicesOf = employees.ToDictionary(
            e => e.Id,
            e => services.Where(s => offered.Any(o => o.EmployeeId == e.Id && o.ServiceId == s.Id)).ToList());

        var reservations = new List<Reservation>();
        for (var offset = -HistoryDays; offset <= 7; offset++)
        {
            var date = today.AddDays(offset);
            if (date.DayOfWeek == DayOfWeek.Sunday) continue;
            var closes = date.DayOfWeek == DayOfWeek.Saturday ? 14 * 60 : 17 * 60;

            foreach (var employee in employees)
            {
                var offer = servicesOf[employee.Id];
                if (offer.Count == 0) continue;

                var cursor = 9 * 60;
                while (cursor < closes)
                {
                    if (cursor >= 12 * 60 && cursor < 12 * 60 + 30 && date.DayOfWeek != DayOfWeek.Saturday)
                    {
                        cursor = 12 * 60 + 30;
                        continue;
                    }

                    var wanted = offset <= 0 ? 0.5 : 0.28;
                    if (rnd.NextDouble() >= wanted)
                    {
                        cursor += 15;
                        continue;
                    }

                    var chosen = Pick(rnd, offer);
                    var duration = chosen.Sum(s => s.DurationMinutes);
                    var breakClash = cursor < 12 * 60 + 30 && cursor + duration > 12 * 60 && date.DayOfWeek != DayOfWeek.Saturday;
                    if (breakClash) cursor = 12 * 60 + 30;
                    if (cursor + duration > closes) break;

                    var start = LocalTime.ToUtc(date, TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(cursor)), zone);
                    cursor += duration + rnd.Next(0, 3) * 15;

                    var client = clients[rnd.Next(clients.Count)];
                    var reservation = BuildReservation(rnd, client.Id, employee.Id, chosen, start, duration, settings.SlotMinutes, now);
                    if (reservation is not null) reservations.Add(reservation);
                }
            }
        }

        db.Reservations.AddRange(reservations);
        await db.SaveChangesAsync(ct);

        var built = BuildOrders(rnd, clients, products, now);
        db.Payments.AddRange(built.Where(b => b.Payment is not null).Select(b => b.Payment!));
        await db.SaveChangesAsync(ct);
        foreach (var (order, payment) in built) order.PaymentId = payment?.Id;
        var orders = built.Select(b => b.Order).ToList();
        db.Orders.AddRange(orders);
        await db.SaveChangesAsync(ct);

        await SeedReviewsAsync(rnd, reservations, orders, now, ct);
        await SeedMessagesAsync(clients, now, ct);
        log.LogInformation(
            "Seeded demo activity: {Reservations} reservations, {Orders} orders", reservations.Count, orders.Count);
    }

    private static List<Service> Pick(Random rnd, List<Service> offer)
    {
        var first = offer[rnd.Next(offer.Count)];
        if (rnd.NextDouble() < 0.18 && offer.Count > 1)
        {
            var second = offer[rnd.Next(offer.Count)];
            if (second.Id != first.Id) return [first, second];
        }
        return [first];
    }

    private static Reservation? BuildReservation(
        Random rnd, string clientId, string employeeId, List<Service> chosen, DateTime startUtc, int duration, int step, DateTime now)
    {
        var past = startUtc < now;
        ReservationStatus status;
        if (past)
        {
            var roll = rnd.NextDouble();
            status = roll < 0.86 ? ReservationStatus.Completed : roll < 0.91 ? ReservationStatus.NoShow : ReservationStatus.Cancelled;
        }
        else
        {
            // Nothing that starts within the next half hour: it could not be booked that late anyway.
            if (startUtc < now.AddMinutes(30)) return null;
            status = rnd.NextDouble() < 0.7 ? ReservationStatus.Confirmed : ReservationStatus.Pending;
        }

        var cancelled = status == ReservationStatus.Cancelled;
        var reservation = new Reservation
        {
            ClientId = clientId,
            EmployeeId = employeeId,
            StartUtc = startUtc,
            EndUtc = startUtc.AddMinutes(duration),
            Status = status,
            Note = rnd.NextDouble() < 0.15 ? "Molim kraće sa strane." : null,
            TotalPrice = chosen.Sum(s => s.Price),
            TotalDurationMinutes = duration,
            CreatedAtUtc = startUtc.AddDays(-rnd.Next(1, 6)),
            Items = chosen.Select(s => new ReservationItem
            {
                ServiceId = s.Id,
                ServiceName = s.Name,
                Price = s.Price,
                DurationMinutes = s.DurationMinutes,
            }).ToList(),
        };
        if (status is ReservationStatus.Confirmed or ReservationStatus.Completed or ReservationStatus.NoShow)
        {
            reservation.ConfirmedAtUtc = reservation.CreatedAtUtc;
            reservation.ConfirmedBy = nameof(ActorKind.System);
        }
        if (status is ReservationStatus.Completed or ReservationStatus.NoShow)
        {
            reservation.CompletedAtUtc = reservation.EndUtc;
            reservation.CompletedBy = nameof(ActorKind.Employee);
        }
        if (cancelled)
        {
            reservation.CancelledAtUtc = startUtc.AddDays(-1);
            reservation.CancelledBy = rnd.NextDouble() < 0.7 ? nameof(ActorKind.Client) : nameof(ActorKind.Admin);
            reservation.CancelReason = reservation.CancelledBy == nameof(ActorKind.Admin)
                ? "Frizer je bolestan, termin je otkazan."
                : rnd.NextDouble() < 0.5 ? "Promjena planova." : null;
        }
        else
        {
            reservation.Slots = SlotGrid.Occupied(startUtc, duration, step)
                .Select(t => new ReservationSlot { EmployeeId = employeeId, SlotStartUtc = t }).ToList();
        }
        return reservation;
    }

    private static List<(Order Order, Payment? Payment)> BuildOrders(Random rnd, IReadOnlyList<AppUser> clients, IReadOnlyList<Product> products, DateTime now)
    {
        var orders = new List<(Order, Payment?)>();
        for (var i = 0; i < 40; i++)
        {
            var createdAt = now.AddDays(-rnd.Next(0, HistoryDays)).AddMinutes(-rnd.Next(0, 600));
            // The first order is always a fresh one waiting for pickup, so every outcome exists whatever the dice say.
            if (i == 0) createdAt = now.AddHours(-5);
            var recent = createdAt > now.AddDays(-4);
            var roll = rnd.NextDouble();
            var status = i == 0 ? OrderStatus.Placed
                : recent && roll < 0.5 ? OrderStatus.Placed : roll < 0.88 ? OrderStatus.Fulfilled : OrderStatus.Cancelled;
            var card = rnd.NextDouble() < 0.3;

            // Orders concentrate on the shop's first products, so "bought together" and the top-products report have shape.
            var lines = products.Take(40).OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4))
                .Select(p => new OrderItem { ProductId = p.Id, ProductName = p.Name, UnitPrice = p.Price, Quantity = rnd.Next(1, 3) })
                .ToList();
            var total = lines.Sum(l => l.UnitPrice * l.Quantity);

            var order = new Order
            {
                ClientId = clients[rnd.Next(clients.Count)].Id,
                Status = status,
                Fulfilment = FulfilmentType.Pickup,
                PaymentMethod = card ? PaymentMethod.Card : PaymentMethod.OnReceipt,
                TotalPrice = total,
                CreatedAtUtc = createdAt,
                Items = lines,
            };
            Payment? payment = null;
            if (card && status != OrderStatus.Cancelled)
            {
                order.PaidAtUtc = createdAt.AddMinutes(2);
                payment = new Payment
                {
                    ProviderRef = $"pi_demo_{i:000}",
                    Amount = total,
                    Status = PaymentStatus.Succeeded,
                    CreatedAtUtc = createdAt,
                };
            }
            if (status == OrderStatus.Fulfilled) order.FulfilledAtUtc = createdAt.AddDays(rnd.Next(1, 3));
            if (status == OrderStatus.Cancelled)
            {
                order.CancelledAtUtc = createdAt.AddHours(3);
                order.CancelledBy = "Client";
            }
            orders.Add((order, payment));
        }
        return orders;
    }

    private async Task SeedReviewsAsync(
        Random rnd, IReadOnlyList<Reservation> reservations, IReadOnlyList<Order> orders, DateTime now, CancellationToken ct)
    {
        var seen = new HashSet<(string, ReviewTargetType, string)>();
        var reviews = new List<Review>();

        void Add(string clientId, ReviewTargetType type, string key, DateTime at, string[] comments)
        {
            if (!seen.Add((clientId, type, key))) return;
            var rating = rnd.NextDouble() switch { < 0.55 => 5, < 0.85 => 4, < 0.93 => 3, < 0.97 => 2, _ => 1 };
            var review = new Review
            {
                ClientId = clientId,
                Rating = rating,
                Comment = rnd.NextDouble() < 0.75 ? comments[rnd.Next(comments.Length)] : null,
                CreatedAtUtc = at.AddDays(1),
            };
            review.SetTarget(type, key);
            if (review.Comment is not null && rating >= 4 && rnd.NextDouble() < 0.25)
            {
                review.ReplyText = "Hvala na povjerenju, vidimo se opet!";
                review.ReplyAtUtc = at.AddDays(2);
            }
            reviews.Add(review);
        }

        // Only people who really were there can have rated: completed appointments and collected orders.
        foreach (var r in reservations.Where(r => r.Status == ReservationStatus.Completed && r.StartUtc < now.AddDays(-2)).OrderBy(_ => rnd.Next()).Take(60))
        {
            Add(r.ClientId, ReviewTargetType.Employee, r.EmployeeId, r.StartUtc, EmployeeComments);
            Add(r.ClientId, ReviewTargetType.Service, r.Items[0].ServiceId.ToString(), r.StartUtc, ServiceComments);
        }
        foreach (var o in orders.Where(o => o.Status == OrderStatus.Fulfilled).OrderBy(_ => rnd.Next()).Take(14))
            Add(o.ClientId, ReviewTargetType.Product, o.Items[0].ProductId.ToString(), o.FulfilledAtUtc ?? o.CreatedAtUtc, ProductComments);

        // One review the salon took down, so moderation has both states to show (it counts for nothing in ratings).
        var hidden = reviews.FirstOrDefault(r => r.Comment is not null && r.Rating <= 3 && r.ReplyText is null)
                     ?? reviews.FirstOrDefault(r => r.Comment is not null && r.ReplyText is null);
        if (hidden is not null) hidden.IsHidden = true;

        db.Reviews.AddRange(reviews);
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedMessagesAsync(IReadOnlyList<AppUser> clients, DateTime now, CancellationToken ct)
    {
        var admin = (await (
            from ur in db.UserRoles
            join role in db.Roles on ur.RoleId equals role.Id
            join u in db.Users on ur.UserId equals u.Id
            where role.Name == Roles.Admin
            select u.Id).FirstOrDefaultAsync(ct)) ?? clients[0].Id;

        var news = new[]
        {
            new Announcement
            {
                Title = "Nova usluga: brijanje vrućim ručnikom",
                Body = "Od ovog mjeseca nudimo tradicionalno brijanje s toplim ručnikom. Rezervišite u aplikaciji.",
                Audience = AnnouncementAudience.Clients,
                Category = AnnouncementCategory.News,
                CreatedAtUtc = now.AddDays(-9),
            },
            new Announcement
            {
                Title = "Radno vrijeme za praznike",
                Body = "U danima državnih praznika radimo od 9 do 13 sati. Termine možete rezervisati unaprijed.",
                Audience = AnnouncementAudience.All,
                Category = AnnouncementCategory.Notice,
                CreatedAtUtc = now.AddDays(-4),
                ExpiresAtUtc = now.AddDays(21),
            },
            new Announcement
            {
                Title = "Popust 10% na proizvode za bradu",
                Body = "Ove sedmice svi proizvodi iz kategorije Njega brade su 10% jeftiniji pri preuzimanju u salonu.",
                Audience = AnnouncementAudience.Clients,
                Category = AnnouncementCategory.Promotion,
                CreatedAtUtc = now.AddDays(-1),
            },
        }.Concat(SeedCatalog.Load().Announcements.Select(a => new Announcement
        {
            Title = a.Title,
            Body = a.Body,
            Audience = Enum.Parse<AnnouncementAudience>(a.Audience),
            ImageKey = a.ImageKey,
            Category = AnnouncementCategory.Notice,
            CreatedAtUtc = now.AddDays(-2),
            ExpiresAtUtc = now.AddDays(10),
        })).ToArray();
        foreach (var a in news)
        {
            a.CreatedByUserId = admin;
            a.RecipientCount = clients.Count;
        }
        db.Announcements.AddRange(news);
        await db.SaveChangesAsync(ct);

        foreach (var a in news)
            db.UserNotifications.AddRange(clients.Select(c => new UserNotification
            {
                UserId = c.Id,
                Title = a.Title,
                Body = a.Body,
                Kind = "Announcement",
                CreatedAtUtc = a.CreatedAtUtc,
                AnnouncementId = a.Id,
                ReadAtUtc = a.CreatedAtUtc < now.AddDays(-5) ? a.CreatedAtUtc.AddHours(5) : null,
            }));
        await db.SaveChangesAsync(ct);
    }
}
