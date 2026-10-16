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

internal sealed class FakeCallLog : ICallLog
{
    public List<CallRecord> Written { get; } = [];
    public CancellationToken LastToken { get; private set; }
    public bool ThrowOnWrite { get; init; }

    public Task WriteAsync(CallRecord record, CancellationToken cancellationToken)
    {
        LastToken = cancellationToken;
        if (ThrowOnWrite)
            throw new InvalidOperationException("database is down");
        Written.Add(record);
        return Task.CompletedTask;
    }
}
