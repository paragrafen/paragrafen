using OpenAI.Embeddings;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Models;

namespace Paragrafen.Data.Ai;

public class EmbeddingService(EmbeddingClient embeddingClient) : IEmbeddingService
{
    private const int Dimensions = 1536;

    public Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken) =>
        AiCalls.CallAsync(AiCalls.Embedding, "embedding model", async () =>
        {
            OpenAIEmbeddingCollection embeddings = await embeddingClient.GenerateEmbeddingsAsync(
                [text],
                new EmbeddingGenerationOptions { Dimensions = Dimensions },
                cancellationToken);

            return new EmbeddingResult(
                embeddings[0].ToFloats(),
                embeddings.Model,
                embeddings.Usage.InputTokenCount);
        });
}
