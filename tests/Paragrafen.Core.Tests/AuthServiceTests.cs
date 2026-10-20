using Microsoft.Extensions.Logging.Testing;
using Paragrafen.Core.Authorization;
using Paragrafen.Core.Contracts.Authentication;
using Paragrafen.Core.Exceptions;
using Paragrafen.Core.Services;

namespace Paragrafen.Core.Tests;

public class AuthServiceTests
{
    private const string Email = "ola@example.com";
    private const string Password = "password123";

    private static AuthService CreateService(FakeIdentityService? identity = null) =>
        new(identity ?? new FakeIdentityService(), new FakeTokenService(), new FakeLogger<AuthService>());

    private static RegisterRequest NewUser() => new(Email, "ola", Password);

    [Fact]
    public async Task Register_with_a_new_email_returns_a_token_for_the_user()
    {
        var response = await CreateService().RegisterAsync(NewUser(), CancellationToken.None);

        Assert.Equal(Email, response.User.Email);
        Assert.Equal([Roles.User], response.User.Roles);
        Assert.Equal($"token-for-{response.User.Id}", response.Token);
    }

    [Fact]
    public async Task Register_with_an_existing_email_is_a_conflict()
    {
        var service = CreateService();
        await service.RegisterAsync(NewUser(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.RegisterAsync(NewUser(), CancellationToken.None));

        Assert.Equal("email_already_registered", ex.Code);
    }

    [Fact]
    public async Task Register_with_a_taken_username_is_a_conflict()
    {
        var identity = new FakeIdentityService { CreateErrors = [new("DuplicateUserName", "Username 'ola' is already taken.")] };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(identity).RegisterAsync(NewUser(), CancellationToken.None));

        Assert.Equal("username_taken", ex.Code);
    }

    [Fact]
    public async Task Login_with_the_right_password_returns_a_token()
    {
        var service = CreateService();
        var registered = await service.RegisterAsync(NewUser(), CancellationToken.None);

        var response = await service.LoginAsync(new LoginRequest(Email, Password), CancellationToken.None);

        Assert.Equal(registered.User.Id, response.User.Id);
        Assert.Equal($"token-for-{registered.User.Id}", response.Token);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_unauthorized()
    {
        var service = CreateService();
        await service.RegisterAsync(NewUser(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginRequest(Email, "wrong-password"), CancellationToken.None));

        Assert.Equal("invalid_credentials", ex.Code);
    }

    [Fact]
    public async Task Login_with_an_unknown_email_gives_the_same_error_as_a_wrong_password()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateService().LoginAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        Assert.Equal("invalid_credentials", ex.Code);
    }
}
