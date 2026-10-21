using System.ClientModel;
using Azure.Identity;
using Paragrafen.Core.Exceptions;

namespace Paragrafen.Data.Ai;

internal static class AiCalls
{
    public static async Task<T> CallAsync<T>(Func<Task<T>> call)
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
