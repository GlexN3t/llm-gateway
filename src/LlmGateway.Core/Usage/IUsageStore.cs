namespace LlmGateway.Core.Usage;

public interface IUsageStore
{
    Task RecordAsync(UsageRecord record, CancellationToken ct = default);

    Task<UsageSummary> GetSummaryAsync(string tenantId, int take, CancellationToken ct = default);
}
