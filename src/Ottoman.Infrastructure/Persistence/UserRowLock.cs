using Microsoft.EntityFrameworkCore;
using Ottoman.Infrastructure.Identity;

namespace Ottoman.Infrastructure.Persistence;

public static class UserRowLock
{
    /// <summary>
    /// Serialises per-account limit checks ("at most N open things per client"): takes an update lock on the
    /// account's user row that is held until the surrounding transaction ends. A second request of the same account
    /// waits here, so its count sees the first one's rows instead of racing past the limit. Other accounts are not
    /// affected. Must be called inside a transaction.
    /// </summary>
    public static async Task LockUserAsync(this OttomanDbContext db, string userId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("LockUserAsync needs an open transaction, or the lock is released at once.");

        // The table name comes from the model, never from input; the id is a parameter.
        var sql = "SELECT 1 FROM [" + db.Model.FindEntityType(typeof(AppUser))!.GetTableName()
                  + "] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE [Id] = {0}";
        await db.Database.ExecuteSqlRawAsync(sql, [userId], ct);
    }
}
