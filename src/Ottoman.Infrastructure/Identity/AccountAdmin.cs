using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Accounts;
using Ottoman.Application.Auth;
using Ottoman.Application.Common;
using Ottoman.Domain;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Identity;

public class AccountAdmin(UserManager<AppUser> users, OttomanDbContext db, IAccessGate gate, TimeProvider clock) : IAccountAdmin
{
    public async Task<Result<AccountLockDto>> UnlockAsync(string id, CancellationToken ct)
    {
        var user = await FindManagedAsync(id);
        if (user is null) return Result<AccountLockDto>.NotFound();

        // The lock and the wrong-password count are both cleared, so the next wrong password starts again from one.
        Ensure(await users.SetLockoutEndDateAsync(user, null));
        Ensure(await users.ResetAccessFailedCountAsync(user));
        return Result<AccountLockDto>.Ok(new AccountLockDto(user.Id, null));
    }

    public async Task<Result<bool>> SetPasswordAsync(string id, string newPassword, CancellationToken ct)
    {
        var user = await FindManagedAsync(id);
        if (user is null) return Result<bool>.NotFound();

        // Identity checks the strength policy and rotates the security stamp; the token is made and spent here.
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded) return Result<bool>.Invalid(string.Join(" ", result.Errors.Select(e => e.Description)));

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);

        // The password changed behind the owner's back: every session ends and no phone keeps receiving pushes.
        var now = clock.GetUtcNow().UtcDateTime;
        await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedUtc, now), ct);
        await db.DeviceTokens.Where(d => d.UserId == user.Id).ExecuteDeleteAsync(ct);
        gate.Invalidate(user.Id);
        return Result<bool>.Ok(true);
    }

    /// <summary>A client's or barber's account; the admin's own and unknown ids are not managed from here.</summary>
    private async Task<AppUser?> FindManagedAsync(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return null;
        var roles = await users.GetRolesAsync(user);
        return roles.Contains(Roles.Client) || roles.Contains(Roles.Employee) ? user : null;
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Unlocking the account failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
