using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Exceptions;
using Paragrafen.Core.Models;

namespace Paragrafen.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class AskCallLogTests(PostgresFixture db)
{
    private const string Question = "Hvor gammel må man være for å stemme?";
    private static readonly ModelResult Usage = new(
        "<status>NOT_FOUND</status><answer>Jeg fant ingen lovtekst om dette.</answer>",
        "gpt-6-luna-2026-09-22",
        1000,
        400,
        500,
        300
    );

    [Fact]
    public async Task Answered_question_is_logged_with_the_response_ids_and_configured_price()
    {
        using var api = CreateApi(new StubAiModelService(() => Usage));

        var response = await api.CreateClient()
            .PostAsJsonAsync("/ask", new { question = Question });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = await db.ReadCallAsync(body.GetProperty("request_id").GetGuid());
        Assert.Equal(body.GetProperty("conversation_id").GetGuid(), row["conversation_id"]);
        Assert.Equal("not_found", row["status"]);
        Assert.Equal((short)1, row["attempts"]);
        Assert.Equal("gpt-6-luna-2026-09-22", row["model"]);
        Assert.Equal(1000, row["input_tokens"]);
        Assert.Equal(0.027m, row["cost_nok"]);
        var askedAt = body.GetProperty("asked_at").GetDateTime().ToUniversalTime();
        Assert.Equal(
            askedAt,
            ((DateTime)row["created_at"]).ToUniversalTime(),
            TimeSpan.FromMilliseconds(1)
        );
    }

    [Fact]
    public async Task Invalid_question_is_logged_under_the_request_id_in_the_error_response()
    {
        using var api = CreateApi(new StubAiModelService(() => Usage));

        var response = await api.CreateClient().PostAsJsonAsync("/ask", new { question = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = await db.ReadCallAsync(body.GetProperty("request_id").GetGuid());
        Assert.Equal("error", row["status"]);
        Assert.Equal("empty_question", row["failure_reason"]);
        Assert.Equal((short)0, row["attempts"]);
    }

    [Fact]
    public async Task Model_failure_is_logged_under_the_request_id_in_the_error_response()
    {
        using var api = CreateApi(
            new StubAiModelService(() =>
                throw new ServiceUnavailableException("model_busy", "busy")
            )
        );

        var response = await api.CreateClient()
            .PostAsJsonAsync("/ask", new { question = Question });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = await db.ReadCallAsync(body.GetProperty("request_id").GetGuid());
        Assert.Equal("error", row["status"]);
        Assert.Equal("model_busy", row["failure_reason"]);
        Assert.Equal((short)1, row["attempts"]);
    }

    private WebApplicationFactory<Program> CreateApi(IAiModelService model) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder
                .UseSetting("ConnectionStrings:Paragrafen", db.ConnectionString)
                .UseSetting("Jwt:IssuerSigningKey", new string('k', 32))
                .UseSetting("Foundry:ChatClient", "test-model")
                .UseSetting("Foundry:Prices:test-model:InputPerMillionNok", "10")
                .UseSetting("Foundry:Prices:test-model:CachedInputPerMillionNok", "2.5")
                .UseSetting("Foundry:Prices:test-model:OutputPerMillionNok", "40")
                .ConfigureTestServices(services =>
                {
                    services.AddSingleton(model);
                    services.AddSingleton<IEmbeddingService, StubEmbeddingService>();
                })
        );

    private sealed class StubAiModelService(Func<ModelResult> complete) : IAiModelService
    {
        public Task<ModelResult> CompleteAsync(
            string prompt,
            string message,
            CancellationToken cancellationToken
        ) => Task.FromResult(complete());
    }
    private sealed class StubEmbeddingService : IEmbeddingService
    {
        public Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(new EmbeddingResult(new float[1536], "text-embedding-3-small", 0));
    }
}
