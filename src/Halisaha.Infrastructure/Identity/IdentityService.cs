using Halisaha.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Infrastructure.Identity;

internal sealed class IdentityService(UserManager<AppUser> users, TimeProvider clock) : IIdentityService
{
    public async Task<Guid> CreateUserAsync(string email, string password, string firstName, string lastName, string? phoneNumber, CancellationToken ct)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            CreatedAt = clock.GetUtcNow(),
        };
        ThrowIfFailed(await users.CreateAsync(user, password));
        ThrowIfFailed(await users.AddToRoleAsync(user, Roles.Player));
        return user.Id;
    }

    public async Task<AuthenticatedUser?> CheckCredentialsAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null) return null;
        if (await users.IsLockedOutAsync(user)) return null;

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user); // counts toward lockout
            return null;
        }
        await users.ResetAccessFailedCountAsync(user);
        var roles = await users.GetRolesAsync(user);
        return new AuthenticatedUser(user.Id, user.Email!, roles.ToList());
    }

    public async Task<IReadOnlyDictionary<Guid, UserName>> GetNamesAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        return await users.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UserName(u.FirstName, u.LastName), ct);
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded) return;
        var errors = result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        throw new IdentityValidationException(errors);
    }
}
