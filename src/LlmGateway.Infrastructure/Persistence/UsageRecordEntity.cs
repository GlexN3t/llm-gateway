using LlmGateway.Core.Usage;

namespace LlmGateway.Infrastructure.Persistence;

public sealed class UsageRecordEntity
{
    public long Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public decimal CostUsd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool Cached { get; set; }

    public UsageRecord ToDomain() => new(Id, TenantId, Model, Provider, PromptTokens, CompletionTokens, CostUsd, CreatedAt, Cached);

    public static UsageRecordEntity FromDomain(UsageRecord record) => new()
    {
        Id = record.Id,
        TenantId = record.TenantId,
        Model = record.Model,
        Provider = record.Provider,
        PromptTokens = record.PromptTokens,
        CompletionTokens = record.CompletionTokens,
        CostUsd = record.CostUsd,
        CreatedAt = record.CreatedAt,
        Cached = record.Cached,
    };
}
