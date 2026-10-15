namespace Ottoman.Application.Common;

public enum ResultStatus
{
    Ok,
    NotFound,
    Conflict,
    Invalid,
}

/// <summary>Outcome of an application operation; controllers translate the status to HTTP.</summary>
public record Result<T>(ResultStatus Status, T? Value = default, string? Error = null)
{
    public bool Succeeded => Status == ResultStatus.Ok;

    public static Result<T> Ok(T value) => new(ResultStatus.Ok, value);
    public static Result<T> NotFound(string? message = null) => new(ResultStatus.NotFound, Error: message ?? "Podatak nije pronađen.");
    public static Result<T> Conflict(string message) => new(ResultStatus.Conflict, Error: message);
    public static Result<T> Invalid(string message) => new(ResultStatus.Invalid, Error: message);
}
