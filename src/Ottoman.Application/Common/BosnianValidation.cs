using System.Globalization;
using FluentValidation;

namespace Ottoman.Application.Common;

/// <summary>
/// Every validation message the API returns is Bosnian: FluentValidation's own default texts come from its built-in
/// Bosnian translation, and a property without an explicit <c>WithName</c> is named in Bosnian instead of by its C#
/// name ("Reason" becomes "Razlog"). Rules with their own <c>WithMessage</c> are unaffected.
/// </summary>
public static class BosnianValidation
{
    public static readonly CultureInfo Culture = new("bs");

    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["Address"] = "Adresa",
        ["Bio"] = "Opis",
        ["Body"] = "Tekst",
        ["Caption"] = "Opis slike",
        ["CategoryId"] = "Kategorija",
        ["Code"] = "Kod",
        ["Comment"] = "Komentar",
        ["CurrentPassword"] = "Trenutna lozinka",
        ["Currency"] = "Valuta",
        ["Days"] = "Dani",
        ["Delta"] = "Promjena",
        ["Description"] = "Opis",
        ["DurationMinutes"] = "Trajanje",
        ["Email"] = "Email",
        ["EmployeeId"] = "Uposlenik",
        ["EndUtc"] = "Kraj",
        ["FirstName"] = "Ime",
        ["From"] = "Od",
        ["Gender"] = "Spol",
        ["Hours"] = "Radno vrijeme",
        ["Ids"] = "Stavke",
        ["Items"] = "Stavke",
        ["Kind"] = "Vrsta",
        ["LastName"] = "Prezime",
        ["Name"] = "Naziv",
        ["NewPassword"] = "Nova lozinka",
        ["Note"] = "Napomena",
        ["Password"] = "Lozinka",
        ["Phone"] = "Telefon",
        ["Price"] = "Cijena",
        ["ProductIds"] = "Proizvodi",
        ["Quantity"] = "Količina",
        ["Rating"] = "Ocjena",
        ["Reason"] = "Razlog",
        ["RefreshToken"] = "Token",
        ["ServiceIds"] = "Usluge",
        ["StartUtc"] = "Početak",
        ["Status"] = "Status",
        ["Stock"] = "Zaliha",
        ["Text"] = "Tekst",
        ["Title"] = "Naslov",
        ["To"] = "Do",
        ["Token"] = "Kod",
    };

    /// <summary>The Bosnian name of a request property, or null to keep FluentValidation's default.</summary>
    public static string? NameOf(string? member) => member is not null && Names.TryGetValue(member, out var n) ? n : null;

    /// <summary>Global and idempotent: call once at start-up (and in tests that build validators by hand).</summary>
    public static void Configure()
    {
        ValidatorOptions.Global.LanguageManager.Enabled = true;
        ValidatorOptions.Global.LanguageManager.Culture = Culture;
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) => NameOf(member?.Name);
    }
}
