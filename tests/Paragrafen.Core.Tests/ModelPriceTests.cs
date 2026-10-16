using Paragrafen.Core.Models;

namespace Paragrafen.Core.Tests;

public class ModelPriceTests
{
    private static readonly ModelPrice Price = new(10m, 2.5m, 40m);

    [Fact]
    public void Cached_input_tokens_use_the_cached_price()
    {
        var result = new ModelResult("svar", "gpt-6-luna", InputTokens: 1000, CachedTokens: 400, OutputTokens: 500, ReasoningTokens: 300);

        Assert.Equal(0.027m, Price.Cost(result));
    }

    [Fact]
    public void No_tokens_cost_nothing()
    {
        var result = new ModelResult("", "gpt-6-luna", 0, 0, 0, 0);

        Assert.Equal(0m, Price.Cost(result));
    }
}
