using Microsoft.AspNetCore.Identity;
using Ottoman.Application.Accounts;
using Ottoman.Application.Common;
using Ottoman.Domain;

namespace Ottoman.Infrastructure.Identity;

public class AccountAdmin(UserManager<AppUser> users) : IAccountAdmin
{
    public async Task<Result<AccountLockDto>> UnlockAsync(string id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return Result<AccountLockDto>.NotFound();
        var roles = await users.GetRolesAsync(user);
        if (!roles.Contains(Roles.Client) && !roles.Contains(Roles.Employee)) return Result<AccountLockDto>.NotFound();

        // The lock and the wrong-password count are both cleared, so the next wrong password starts again from one.
        Ensure(await users.SetLockoutEndDateAsync(user, null));
        Ensure(await users.ResetAccessFailedCountAsync(user));
        return Result<AccountLockDto>.Ok(new AccountLockDto(user.Id, null));
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Unlocking the account failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
