namespace Ottoman.Application.Common;

/// <summary>
/// An expected failure the caller can act on. The message is written for the user (Bosnian) and is sent to the client
/// as the problem detail, so it must never carry internal details. The API maps each subtype to one HTTP status.
/// </summary>
public abstract class OttomanException(string message) : Exception(message);

/// <summary>The thing the caller asked for does not exist, or is not visible to them (404).</summary>
public sealed class NotFoundException(string? message = null)
    : OttomanException(message ?? ProblemTexts.NotFoundDetail);

/// <summary>The request is well-formed but its content is not acceptable (400).</summary>
public class InvalidRequestException(string message) : OttomanException(message);

/// <summary>The request clashes with the current state of the data, e.g. a duplicate or a lost race (409).</summary>
public sealed class ConflictException(string message) : OttomanException(message);

/// <summary>The caller is signed in but not allowed to do this (403).</summary>
public sealed class ForbiddenException(string? message = null)
    : OttomanException(message ?? ProblemTexts.ForbiddenDetail);

/// <summary>The caller is not signed in, or the credentials are not valid (401).</summary>
public sealed class UnauthorizedException(string? message = null)
    : OttomanException(message ?? ProblemTexts.UnauthorizedDetail);

/// <summary>A business rule forbids the operation in the current state, e.g. cancelling a shipped order (422).</summary>
public sealed class BusinessRuleException(string message) : OttomanException(message);

/// <summary>One place for the Bosnian titles and statuses of failures, shared by exceptions and <see cref="Result{T}"/>.</summary>
public static class ProblemTexts
{
    public const string NotFoundTitle = "Nije pronađeno.";
    public const string NotFoundDetail = "Podatak nije pronađen.";
    public const string ConflictTitle = "Sukob podataka.";
    public const string InvalidTitle = "Podaci nisu ispravni.";
    public const string ForbiddenTitle = "Zabranjeno.";
    public const string ForbiddenDetail = "Nemate pravo za ovu radnju.";
    public const string UnauthorizedTitle = "Prijava je obavezna.";
    public const string UnauthorizedDetail = "Prijavite se za nastavak.";
    public const string BusinessRuleTitle = "Radnja nije dozvoljena.";
    public const string ServerErrorTitle = "Došlo je do greške.";
    public const string ServerErrorDetail = "Nešto je pošlo po zlu na serveru. Pokušajte ponovo kasnije.";

    /// <summary>The HTTP status, title and (user-safe) detail for an exception; unknown exceptions give a generic 500.</summary>
    public static (int Status, string Title, string? Detail) Describe(Exception exception) => exception switch
    {
        NotFoundException e => (404, NotFoundTitle, e.Message),
        ConflictException e => (409, ConflictTitle, e.Message),
        ForbiddenException e => (403, ForbiddenTitle, e.Message),
        UnauthorizedException e => (401, UnauthorizedTitle, e.Message),
        BusinessRuleException e => (422, BusinessRuleTitle, e.Message),
        InvalidRequestException e => (400, InvalidTitle, e.Message),
        _ => (500, ServerErrorTitle, ServerErrorDetail),
    };

    /// <summary>The HTTP status and title of a failed <see cref="ResultStatus"/>.</summary>
    public static (int Status, string Title) Describe(ResultStatus status) => status switch
    {
        ResultStatus.NotFound => (404, NotFoundTitle),
        ResultStatus.Conflict => (409, ConflictTitle),
        _ => (400, InvalidTitle),
    };
}
