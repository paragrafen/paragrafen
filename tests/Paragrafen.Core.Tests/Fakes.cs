using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Models;

namespace Paragrafen.Core.Tests;

internal sealed class FakeAiModelService(Func<CancellationToken, ModelResult> complete) : IAiModelService
{
    public static FakeAiModelService Returning(ModelResult result) => new(_ => result);

    public static FakeAiModelService Throwing(Exception exception) => new(_ => throw exception);

    public Task<ModelResult> CompleteAsync(string message, CancellationToken cancellationToken) =>
        Task.FromResult(complete(cancellationToken));
}
