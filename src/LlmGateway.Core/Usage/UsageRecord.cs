namespace LlmGateway.Core.Usage;

public sealed record UsageRecord(
    long Id,
    string TenantId,
    string Model,
    string Provider,
    int PromptTokens,
    int CompletionTokens,
    decimal CostUsd,
    DateTimeOffset CreatedAt,
    bool Cached = false);
