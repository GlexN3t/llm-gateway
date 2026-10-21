namespace LlmGateway.Core.Routing;

public sealed class ModelRouter
{
    private readonly IReadOnlyList<(string Pattern, ILlmProvider Provider)> _rules;

    public ModelRouter(IEnumerable<(string Pattern, ILlmProvider Provider)> rules)
    {
        _rules = [.. rules];
    }

    public ILlmProvider Resolve(string model)
    {
        foreach (var (pattern, provider) in _rules)
        {
            if (Matches(pattern, model))
            {
                return provider;
            }
        }

        throw new InvalidOperationException($"No provider route configured for model '{model}'.");
    }

    private static bool Matches(string pattern, string model) =>
        pattern == "*"
        || (pattern.EndsWith('*') && model.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase))
        || string.Equals(pattern, model, StringComparison.OrdinalIgnoreCase);
}
