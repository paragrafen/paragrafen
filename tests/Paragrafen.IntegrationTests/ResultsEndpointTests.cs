using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Paragrafen.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class ResultsEndpointTests(PostgresFixture db)
{
    private const string Password = "password123";
    private const string RoleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    [Fact]
    public async Task Anonymous_request_is_unauthorized()
    {
        using var api = CreateApi();

        var response = await api.CreateClient().GetAsync("/results");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Normal_user_is_forbidden()
    {
        using var api = CreateApi();
        var client = api.CreateClient();
        var email = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, email));

        var response = await client.GetAsync("/results");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_gets_the_results()
    {
        using var api = CreateApi();
        var client = api.CreateClient();
        var email = await RegisterAsync(client);
        await MakeAdminAsync(email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, email));

        var response = await client.GetAsync("/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Number, body.GetProperty("total").GetProperty("calls").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("days").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("recent_calls").ValueKind);
    }

    // The web app reads this claim to decide whether to show the results link.
    [Fact]
    public async Task Token_lists_the_roles_under_the_claim_the_web_app_reads()
    {
        using var api = CreateApi();
        var client = api.CreateClient();
        var email = await RegisterAsync(client);
        await MakeAdminAsync(email);

        var token = await LoginAsync(client, email);

        var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(token.Split('.')[1])).RootElement;
        var roles = payload.GetProperty(RoleClaim).EnumerateArray().Select(r => r.GetString());
        Assert.Contains("Admin", roles);
    }

    private static async Task<string> RegisterAsync(HttpClient client)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/register",
            new { email, user_name = $"u{Guid.NewGuid():N}"[..20], password = Password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        return email;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    // Same SQL as "Making a user admin" in infra/README.md.
    private async Task MakeAdminAsync(string email)
    {
        await using var command = db.DataSource.CreateCommand("""
            INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
            SELECT u."Id", r."Id" FROM "AspNetUsers" u, "AspNetRoles" r
            WHERE u."NormalizedEmail" = upper($1) AND r."NormalizedName" = 'ADMIN'
            """);
        command.Parameters.Add(new NpgsqlParameter { Value = email });
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private WebApplicationFactory<Program> CreateApi() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder
                .UseSetting("ConnectionStrings:Paragrafen", db.ConnectionString)
                .UseSetting("Jwt:IssuerSigningKey", new string('k', 32)));
}
