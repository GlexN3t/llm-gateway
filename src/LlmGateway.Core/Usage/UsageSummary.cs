namespace LlmGateway.Core.Usage;

public sealed record UsageSummary(
    int RequestCount,
    int PromptTokens,
    int CompletionTokens,
    decimal TotalCostUsd,
    int CachedRequestCount,
    IReadOnlyList<UsageRecord> RecentRecords)
{
    public int TotalTokens => PromptTokens + CompletionTokens;
}
