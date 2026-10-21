using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Paragrafen.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class AuthTests(PostgresFixture db)
{
    private const string Password = "password123";

    [Fact]
    public async Task Registered_user_can_log_in()
    {
        using var api = CreateApi();
        var client = api.CreateClient();
        var email = UniqueEmail();

        var register = await client.PostAsJsonAsync("/register", new { email, user_name = UniqueUserName(), password = Password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal("User", body.GetProperty("user").GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task Registering_the_same_email_twice_is_a_conflict()
    {
        using var api = CreateApi();
        var client = api.CreateClient();
        var email = UniqueEmail();
        var first = await client.PostAsJsonAsync("/register", new { email, user_name = UniqueUserName(), password = Password });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/register", new { email, user_name = UniqueUserName(), password = Password });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_unauthorized()
    {
        using var api = CreateApi();
        var client = api.CreateClient();
        var email = UniqueEmail();
        // Without this, a failed registration would also give 401: an unknown email and a wrong password look the same.
        var register = await client.PostAsJsonAsync("/register", new { email, user_name = UniqueUserName(), password = Password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/login", new { email, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Too_short_password_is_rejected()
    {
        using var api = CreateApi();

        var register = await api.CreateClient()
            .PostAsJsonAsync("/register", new { email = UniqueEmail(), user_name = UniqueUserName(), password = "short" });

        // A 400 alone could also mean a misnamed JSON field or an invalid email, so check that the password was the reason.
        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
        var error = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.Contains("Password", error.GetProperty("message").GetString());
    }

    private WebApplicationFactory<Program> CreateApi() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder
                .UseSetting("ConnectionStrings:Paragrafen", db.ConnectionString)
                .UseSetting("Jwt:IssuerSigningKey", new string('k', 32)));

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";
    private static string UniqueUserName() => $"u{Guid.NewGuid():N}"[..20];
}
