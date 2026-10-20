namespace Paragrafen.Core.Models;

public sealed record ModelPrice(
    decimal InputPerMillionNok,
    decimal CachedInputPerMillionNok,
    decimal OutputPerMillionNok
)
{
    public decimal Cost(ModelResult result) =>
        ((result.InputTokens - result.CachedTokens) * InputPerMillionNok
         + result.CachedTokens * CachedInputPerMillionNok
         + result.OutputTokens * OutputPerMillionNok) / 1_000_000m;
}
