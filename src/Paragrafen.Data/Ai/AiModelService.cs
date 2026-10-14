using Paragrafen.Core.Abstractions;
using OpenAI.Chat;
using Azure.Identity;
using System.ClientModel;
using Paragrafen.Core.Exceptions;

namespace Paragrafen.Data.Ai;

public class AiModelService(ChatClient chatClient) : IAiModelService
{

    public Task<string> CompleteAsync(string message, CancellationToken cancellationToken) =>
        CallAsync(async () =>
        {
            ChatCompletion completion = await chatClient.CompleteChatAsync(
                [new UserChatMessage(message)],
                cancellationToken: cancellationToken);

            if (completion.Content.Count == 0 ||  string.IsNullOrWhiteSpace(completion.Content[0].Text))
            {
                throw new BadGatewayException("model_no_answer", "The model did not return anything");
            }

            return completion.Content[0].Text;
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
