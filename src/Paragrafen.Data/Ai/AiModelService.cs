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
    private const int MaxOutputTokens = 10000;

    public Task<ModelResult> CompleteAsync(string prompt, string message, CancellationToken cancellationToken) =>
        AiCalls.CallAsync(AiCalls.Chat, "language model", async () =>
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
                [new SystemChatMessage(prompt), new UserChatMessage(message)],
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
}
