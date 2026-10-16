using Paragrafen.Core.Abstractions;
using OpenAI.Chat;
using Azure.Identity;
using System.ClientModel;
using Azure.AI.OpenAI.Chat;
using Paragrafen.Core.Exceptions;
using Paragrafen.Core.Models;

namespace Paragrafen.Data.Ai;

public class AiModelService(ChatClient chatClient) : IAiModelService
{
    private const int MaxOutputTokens = 1000;

    public Task<ModelResult> CompleteAsync(string message, CancellationToken cancellationToken) =>
        CallAsync(async () =>
        {
            // gpt-6-luna rejects the legacy max_tokens field, which Azure.AI.OpenAI sends by default.
            // This beta opt-in sends max_completion_tokens instead; remove it once a stable version does that by default.
            ChatCompletionOptions requestOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = MaxOutputTokens
            };

#pragma warning disable AOAI001
            requestOptions.SetNewMaxCompletionTokensPropertyEnabled(true);
#pragma warning restore AOAI001

            ChatCompletion completion = await chatClient.CompleteChatAsync(
                [new UserChatMessage(message)],
                requestOptions,
                cancellationToken: cancellationToken);

            return new ModelResult(
                completion.Content.Count > 0 ? completion.Content[0].Text : "",
                completion.Model,
                completion.Usage.InputTokenCount,
                completion.Usage.InputTokenDetails?.CachedTokenCount ?? 0,
                completion.Usage.OutputTokenCount,
                completion.Usage.OutputTokenDetails?.ReasoningTokenCount ?? 0);
        });
    private static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            throw new ServiceUnavailableException(
                "model_busy", "The model is handling too many requests. Try again shortly.", ex);
        }
        catch (Exception ex) when (ex is ClientResultException or AuthenticationFailedException)
        {
            throw new BadGatewayException(
                "model_unavailable", "The language model could not be reached.", ex);
        }
    }
}
