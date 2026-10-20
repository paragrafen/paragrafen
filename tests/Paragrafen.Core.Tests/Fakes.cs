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
    public bool Hang { get; init; }
    public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool WriteCancelled { get; private set; }

    public async Task WriteAsync(CallRecord record, CancellationToken cancellationToken)
    {
        LastToken = cancellationToken;
        cancellationToken.Register(() => WriteCancelled = true);
        WriteStarted.TrySetResult();
        if (ThrowOnWrite)
            throw new InvalidOperationException("database is down");
        if (Hang)
            await Task.Delay(Timeout.Infinite, cancellationToken);
        Written.Add(record);
    }
}
