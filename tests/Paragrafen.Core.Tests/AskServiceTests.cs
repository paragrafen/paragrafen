using Paragrafen.Core.Contracts;
using Paragrafen.Core.Exceptions;
using Paragrafen.Core.Models;
using Paragrafen.Core.Services;

namespace Paragrafen.Core.Tests;

public class AskServiceTests
{
    private const string Question = "Hvor gammel må man være for å stemme?";
    private static readonly ModelResult Usage = new("Du må være 18 år.", "gpt-6-luna-2026-08-01", 1000, 400, 500, 300);

    [Fact]
    public async Task Model_text_is_returned_as_the_answer()
    {
        var response = await new AskService(FakeAiModelService.Returning(Usage))
            .AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7());

        Assert.Equal(AnswerStatus.Answered, response.Status);
        Assert.Equal("Du må være 18 år.", response.Answer);
    }

    [Fact]
    public async Task Empty_model_answer_is_a_bad_gateway()
    {
        var service = new AskService(FakeAiModelService.Returning(Usage with { Text = " " }));

        var ex = await Assert.ThrowsAsync<BadGatewayException>(() =>
            service.AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7()));

        Assert.Equal("model_no_answer", ex.Code);
    }
}
