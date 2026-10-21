using System.ClientModel;
using Azure.Identity;
using Paragrafen.Core.Exceptions;

namespace Paragrafen.Data.Ai;


internal static class AiCalls
{
    public const string Chat = "model";
    public const string Embedding = "embedding";

    public static async Task<T> CallAsync<T>(string codePrefix, string serviceName, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            throw new ServiceUnavailableException(
                $"{codePrefix}_busy", $"The {serviceName} is handling too many requests. Try again shortly.", ex);
        }
        catch (Exception ex) when (ex is ClientResultException or AuthenticationFailedException)
        {
            throw new BadGatewayException(
                $"{codePrefix}_unavailable", $"The {serviceName} could not be reached.", ex);
        }
    }
}
