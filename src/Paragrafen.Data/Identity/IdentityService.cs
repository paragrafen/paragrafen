using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Authorization;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Contracts.Authentication;

namespace Paragrafen.Data.Identity;

public class IdentityService(UserManager<ApplicationUser> userManager) : IIdentityService, IUserLookupService
{
    public async Task<UserResponse?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email);

        return user is null ? null : await ToResponseAsync(user);
    }

    public async Task<CreateUserResult> CreateUserAsync(RegisterRequest registerRequest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = registerRequest.Email,
            UserName = registerRequest.UserName
        };
        var result = await userManager.CreateAsync(user, registerRequest.Password);

        if (result.Succeeded)
        {
            result = await userManager.AddToRolesAsync(user, [Roles.User]);

            // Deletes a user if the role assignment failed, so it doesn't create an invalid user
            if (!result.Succeeded)
                await userManager.DeleteAsync(user);
        }

        return result.Succeeded
            ? new CreateUserResult(Success: true, User: await ToResponseAsync(user), Errors: [])
            : new CreateUserResult(
                Success: false,
                User: null,
                Errors: result.Errors.Select(e =>
                    new CreateUserResult.Error(e.Code, e.Description)).ToArray());
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ids = userIds.Distinct().ToList();
        return await userManager.Users
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName!, cancellationToken);
    }

    public async Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || await userManager.IsLockedOutAsync(user))
            return false;

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return false;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return true;
    }

    // Private helper
    private async Task<UserResponse> ToResponseAsync(ApplicationUser user)
        => new(Id: user.Id, Email: user.Email!, UserName: user.UserName!, Roles:
            [.. await userManager.GetRolesAsync(user)]
        );
}
