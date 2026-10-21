namespace LlmGateway.Core.Usage;

public sealed class CostCalculator(IReadOnlyDictionary<string, ModelPrice> prices)
{
    public decimal Calculate(string model, TokenUsage usage)
    {
        if (!prices.TryGetValue(model, out var price) && !prices.TryGetValue("*", out price))
        {
            return 0m;
        }

        return (usage.PromptTokens * price.InputPerMillion + usage.CompletionTokens * price.OutputPerMillion) / 1_000_000m;
    }
}
