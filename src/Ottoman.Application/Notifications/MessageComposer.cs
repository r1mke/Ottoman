using System.Globalization;
using Ottoman.Contracts;
using Ottoman.Domain;

namespace Ottoman.Application.Notifications;

public record Party(string UserId, string Name, string? Email);

public record ReservationFacts(int Id, Party Client, Party Employee, string LocalStart, string Services, decimal TotalPrice);

/// <param name="Date">The day the order was made, in salon time ("08.11.2026."): how people name an order.</param>
public record OrderFacts(int Id, Party Client, decimal Total, string Items, string Date);

/// <summary>
/// One message for one recipient. Exactly one of <see cref="UserId"/>, <see cref="Email"/> or <see cref="ToAdmins"/>
/// says who gets it; the in-app inbox needs a user, so e-mail-only messages (e.g. to an unregistered address) skip it.
/// </summary>
public record Message(
    string Title,
    string Body,
    string Kind,
    string? UserId = null,
    string? Email = null,
    bool ToAdmins = false,
    bool InApp = true,
    bool SendEmail = true);

/// <summary>Turns domain events into the messages people receive. Pure: no I/O, no clock, easy to test.</summary>
public static class MessageComposer
{
    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture) + " KM";

    // ---------------------------------------------------------------- accounts

    public static IReadOnlyList<Message> Welcome(UserRegistered e) =>
    [
        new("Dobrodošli u Ottoman",
            $"Zdravo {e.FullName},\nvaš račun je kreiran. Sada možete rezervisati termine i naručivati proizvode.",
            "Welcome", UserId: e.UserId, InApp: false),
    ];

    public static IReadOnlyList<Message> ExistingAccountAttempt(RegistrationAttemptedForExistingAccount e) =>
    [
        new("Pokušaj registracije",
            $"Zdravo {e.FullName},\nneko se pokušao registrovati s ovom email adresom, ali račun već postoji. " +
            "Ako ste to bili vi, prijavite se ili iskoristite „Zaboravljena lozinka“. Ako niste, ignorišite ovu poruku.",
            "RegistrationAttempt", Email: e.Email, InApp: false),
    ];

    public static IReadOnlyList<Message> PasswordReset(PasswordResetRequested e, string token) =>
    [
        new("Resetovanje lozinke",
            $"Zdravo {e.FullName},\nprimili smo zahtjev za novu lozinku. U aplikaciji odaberite „Zaboravljena lozinka“ → „Imam kod“ " +
            $"i zalijepite ovaj kod:\n\n{token}\n\nAko niste tražili promjenu, ignorišite ovu poruku.",
            "PasswordReset", UserId: e.UserId, InApp: false),
    ];

    // ---------------------------------------------------------------- reservations

    public static IReadOnlyList<Message> ReservationCreated(ReservationFacts f, bool confirmed = true) => confirmed
        ?
        [
            new("Termin potvrđen",
                $"Vaš termin je potvrđen: {f.LocalStart}, {f.Employee.Name} ({f.Services}). Ukupno {Money(f.TotalPrice)}.",
                "ReservationCreated", UserId: f.Client.UserId),
            new("Nova rezervacija",
                $"{f.Client.Name} je rezervisao/la termin: {f.LocalStart} ({f.Services}).",
                "ReservationCreated", UserId: f.Employee.UserId),
        ]
        :
        [
            new("Zahtjev za termin primljen",
                $"Vaš termin {f.LocalStart}, {f.Employee.Name} ({f.Services}) čeka potvrdu salona. Vrijeme je rezervisano za vas; javićemo vam kad bude potvrđen.",
                "ReservationCreated", UserId: f.Client.UserId),
            new("Nova rezervacija čeka potvrdu",
                $"{f.Client.Name} želi termin: {f.LocalStart} ({f.Services}). Potvrdite ga ili odbijte uz razlog.",
                "ReservationCreated", UserId: f.Employee.UserId),
        ];

    public static IReadOnlyList<Message> ReservationConfirmed(ReservationFacts f) =>
    [
        new("Termin potvrđen",
            $"Salon je potvrdio vaš termin: {f.LocalStart}, {f.Employee.Name} ({f.Services}). Ukupno {Money(f.TotalPrice)}.",
            "ReservationConfirmed", UserId: f.Client.UserId),
    ];

    public static IReadOnlyList<Message> ReservationDeclined(ReservationFacts f, string? reason) =>
    [
        new("Termin nije prihvaćen",
            $"Salon nije mogao prihvatiti vaš termin ({f.LocalStart}, {f.Employee.Name}). Razlog: {reason}. Odaberite drugi termin u aplikaciji.",
            "ReservationCancelled", UserId: f.Client.UserId),
    ];

    public static IReadOnlyList<Message> ReservationCancelled(ReservationFacts f, string cancelledBy, string? reason)
    {
        var why = string.IsNullOrWhiteSpace(reason) ? "" : $" Razlog: {reason}.";
        var list = new List<Message>();
        if (cancelledBy != "Client")
            list.Add(new("Termin otkazan",
                $"Vaš termin ({f.LocalStart}, {f.Employee.Name}) je otkazan od strane salona.{why}",
                "ReservationCancelled", UserId: f.Client.UserId));
        else
            list.Add(new("Termin otkazan",
                $"Otkazali ste termin ({f.LocalStart}, {f.Employee.Name}).",
                "ReservationCancelled", UserId: f.Client.UserId, SendEmail: false));

        if (cancelledBy != "Employee")
            list.Add(new("Termin otkazan",
                $"Termin klijenta {f.Client.Name} ({f.LocalStart}) je otkazan.{why}",
                "ReservationCancelled", UserId: f.Employee.UserId));
        return list;
    }

    public static IReadOnlyList<Message> ReservationRescheduled(ReservationFacts f, string oldLocalStart) =>
    [
        new("Termin pomjeren",
            $"Vaš termin je pomjeren s {oldLocalStart} na {f.LocalStart} ({f.Employee.Name}).",
            "ReservationRescheduled", UserId: f.Client.UserId),
        new("Termin pomjeren",
            $"Termin klijenta {f.Client.Name} je pomjeren s {oldLocalStart} na {f.LocalStart}.",
            "ReservationRescheduled", UserId: f.Employee.UserId),
    ];

    public static IReadOnlyList<Message> ReservationAffectedByTimeOff(ReservationFacts f) =>
    [
        new("Promjena kod vašeg termina",
            $"{f.Employee.Name} neće biti u salonu u vrijeme vašeg termina ({f.LocalStart}). Salon će vas kontaktirati " +
            "da dogovorite novo vrijeme; termin možete i otkazati u aplikaciji.",
            "ReservationAffected", UserId: f.Client.UserId),
    ];

    public static IReadOnlyList<Message> Reminder(ReservationFacts f, ReminderKind kind) =>
    [
        new(kind == ReminderKind.Day ? "Podsjetnik: termin sutra" : "Podsjetnik: termin uskoro",
            $"Podsjećamo vas na termin: {f.LocalStart}, {f.Employee.Name} ({f.Services}).",
            "Reminder", UserId: f.Client.UserId),
    ];

    // ---------------------------------------------------------------- shop

    public static IReadOnlyList<Message> OrderPlaced(OrderFacts f, string method) =>
    [
        new("Narudžba zaprimljena",
            method == "Card"
                ? $"Narudžba od {f.Date} ({f.Items}) čeka plaćanje. Ukupno {Money(f.Total)}."
                : $"Narudžba od {f.Date} ({f.Items}) je zaprimljena. Plaćate pri preuzimanju u salonu: {Money(f.Total)}.",
            "OrderPlaced", UserId: f.Client.UserId, SendEmail: method != "Card"),
        new("Nova narudžba",
            $"Narudžba klijenta {f.Client.Name} ({f.Date}): {f.Items}. {Money(f.Total)}.",
            "OrderPlaced", ToAdmins: true, SendEmail: false),
    ];

    public static IReadOnlyList<Message> OrderPaid(OrderFacts f) =>
    [
        new("Plaćanje primljeno",
            $"Plaćanje za narudžbu od {f.Date} ({f.Items}) je primljeno ({Money(f.Total)}). Proizvode preuzimate u salonu.",
            "OrderPaid", UserId: f.Client.UserId),
        new("Narudžba plaćena",
            $"Narudžba klijenta {f.Client.Name} ({f.Date}) je plaćena karticom: {f.Items}.",
            "OrderPaid", ToAdmins: true, SendEmail: false),
    ];

    public static IReadOnlyList<Message> OrderCancelled(OrderFacts f, string cancelledBy, string? reason = null)
    {
        var why = string.IsNullOrWhiteSpace(reason) ? "" : $" Razlog: {reason.Trim()}.";
        return
        [
            new(cancelledBy == "System" ? "Narudžba istekla" : "Narudžba otkazana",
                cancelledBy == "System"
                    ? $"Narudžba od {f.Date} ({f.Items}) nije plaćena na vrijeme pa je poništena."
                    : $"Narudžba od {f.Date} ({f.Items}) je otkazana.{why} Ako je plaćena karticom, novac se vraća.",
                "OrderCancelled", UserId: f.Client.UserId, SendEmail: cancelledBy != "System"),
        ];
    }

    public static IReadOnlyList<Message> LowStock(ProductLowStock e) =>
    [
        new("Niska zaliha",
            e.Stock == 0
                ? $"„{e.Name}“ je rasprodan."
                : $"„{e.Name}“: ostalo {e.Stock} kom (prag {e.Threshold}).",
            "ProductLowStock", ToAdmins: true),
    ];
}
