using Microsoft.AspNetCore.Identity;

namespace Ottoman.Infrastructure.Identity;

/// <summary>
/// ASP.NET Identity's errors in Bosnian. They reach users when a password change or an account write fails
/// ("Incorrect password." used to come back as is). The codes stay the English ones, only the texts change.
/// </summary>
public sealed class BosnianIdentityErrorDescriber : IdentityErrorDescriber
{
    private static IdentityError E(string code, string text) => new() { Code = code, Description = text };

    public override IdentityError DefaultError() => E(nameof(DefaultError), "Došlo je do greške. Pokušajte ponovo.");
    public override IdentityError ConcurrencyFailure() => E(nameof(ConcurrencyFailure), "Podaci su u međuvremenu promijenjeni. Osvježite i pokušajte ponovo.");
    public override IdentityError PasswordMismatch() => E(nameof(PasswordMismatch), "Pogrešna lozinka.");
    public override IdentityError InvalidToken() => E(nameof(InvalidToken), "Kod nije važeći ili je istekao.");
    public override IdentityError RecoveryCodeRedemptionFailed() => E(nameof(RecoveryCodeRedemptionFailed), "Kod za oporavak nije važeći.");
    public override IdentityError LoginAlreadyAssociated() => E(nameof(LoginAlreadyAssociated), "Ova prijava je već povezana s drugim računom.");
    public override IdentityError InvalidUserName(string? userName) => E(nameof(InvalidUserName), "Korisničko ime nije ispravno.");
    public override IdentityError InvalidEmail(string? email) => E(nameof(InvalidEmail), "Email adresa nije ispravna.");
    public override IdentityError DuplicateUserName(string userName) => E(nameof(DuplicateUserName), "Račun s tom email adresom već postoji.");
    public override IdentityError DuplicateEmail(string email) => E(nameof(DuplicateEmail), "Račun s tom email adresom već postoji.");
    public override IdentityError InvalidRoleName(string? role) => E(nameof(InvalidRoleName), "Naziv uloge nije ispravan.");
    public override IdentityError DuplicateRoleName(string role) => E(nameof(DuplicateRoleName), "Uloga s tim nazivom već postoji.");
    public override IdentityError UserAlreadyHasPassword() => E(nameof(UserAlreadyHasPassword), "Račun već ima lozinku.");
    public override IdentityError UserLockoutNotEnabled() => E(nameof(UserLockoutNotEnabled), "Zaključavanje nije uključeno za ovaj račun.");
    public override IdentityError UserAlreadyInRole(string role) => E(nameof(UserAlreadyInRole), "Korisnik već ima ovu ulogu.");
    public override IdentityError UserNotInRole(string role) => E(nameof(UserNotInRole), "Korisnik nema ovu ulogu.");
    public override IdentityError PasswordTooShort(int length) => E(nameof(PasswordTooShort), $"Lozinka mora imati najmanje {length} znakova.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        E(nameof(PasswordRequiresUniqueChars), $"Lozinka mora imati najmanje {uniqueChars} različitih znakova.");
    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        E(nameof(PasswordRequiresNonAlphanumeric), "Lozinka mora imati barem jedan poseban znak (npr. ! ili #).");
    public override IdentityError PasswordRequiresDigit() => E(nameof(PasswordRequiresDigit), "Lozinka mora imati barem jednu cifru.");
    public override IdentityError PasswordRequiresLower() => E(nameof(PasswordRequiresLower), "Lozinka mora imati barem jedno malo slovo.");
    public override IdentityError PasswordRequiresUpper() => E(nameof(PasswordRequiresUpper), "Lozinka mora imati barem jedno veliko slovo.");
}
