using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Authorization;
using Paragrafen.Core.Contracts.Authentication;

internal sealed class FakeIdentityService : IIdentityService
{
    private readonly Dictionary<string, (UserResponse User, string Password)> _users = new(StringComparer.OrdinalIgnoreCase);

    // Errors CreateUserAsync returns instead of creating the user, like Identity's DuplicateUserName.
    public IReadOnlyList<CreateUserResult.Error> CreateErrors { get; init; } = [];

    public Task<UserResponse?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_users.TryGetValue(email, out var entry) ? entry.User : null);

    public Task<CreateUserResult> CreateUserAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (CreateErrors.Count > 0)
            return Task.FromResult(new CreateUserResult(false, null, CreateErrors));

        var user = new UserResponse(Guid.NewGuid(), request.Email, request.UserName, [Roles.User]);
        _users[request.Email] = (user, request.Password);
        return Task.FromResult(new CreateUserResult(true, user, []));
    }

    public Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.Any(entry => entry.User.Id == userId && entry.Password == password));
}

internal sealed class FakeTokenService : ITokenService
{
    public AccessToken CreateAccessToken(UserResponse user) =>
        new($"token-for-{user.Id}", DateTimeOffset.UnixEpoch.AddHours(1));
}
