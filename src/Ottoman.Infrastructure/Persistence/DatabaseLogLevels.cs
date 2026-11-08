using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Ottoman.Infrastructure.Persistence;

/// <summary>
/// EF Core logs every failed command and save at Error, including the unique-key collisions the code expects and
/// handles (two clients racing for one slot, an Idempotency-Key replay, the same picture twice): those are Warnings.
/// A failure nobody handles still reaches the error log, because the request's unhandled exception is logged at Error
/// by ASP.NET (and background services log their own failures at Error).
/// </summary>
public static class DatabaseLogLevels
{
    public static readonly (EventId, LogLevel)[] FailedCommands =
    [
        (RelationalEventId.CommandError, LogLevel.Warning),
        (CoreEventId.SaveChangesFailed, LogLevel.Warning),
    ];
}
