using LlmGateway.Core;
using LlmGateway.Core.Usage;

namespace LlmGateway.Tests;

public class CostCalculatorTests
{
    private static CostCalculator CreateCalculator() => new(new Dictionary<string, ModelPrice>
    {
        ["gpt-4o-mini"] = new(0.15m, 0.60m),
        ["*"] = new(1m, 1m),
    });

    [Fact]
    public void Computes_cost_from_token_usage()
    {
        var calculator = CreateCalculator();

        var cost = calculator.Calculate("gpt-4o-mini", new TokenUsage(1_000_000, 1_000_000));

        Assert.Equal(0.75m, cost);
    }

    [Fact]
    public void Uses_wildcard_price_for_unknown_model()
    {
        var calculator = CreateCalculator();

        var cost = calculator.Calculate("mystery-model", new TokenUsage(1_000_000, 0));

        Assert.Equal(1m, cost);
    }

    [Fact]
    public void Returns_zero_when_no_price_matches()
    {
        var calculator = new CostCalculator(new Dictionary<string, ModelPrice>());

        Assert.Equal(0m, calculator.Calculate("anything", new TokenUsage(10, 10)));
    }

    [Fact]
    public void Partial_token_counts_scale_correctly()
    {
        var calculator = CreateCalculator();

        var cost = calculator.Calculate("gpt-4o-mini", new TokenUsage(500, 250));

        Assert.Equal(0.000225m, cost);
    }
}
