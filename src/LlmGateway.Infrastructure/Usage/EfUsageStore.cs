using LlmGateway.Core.Usage;
using LlmGateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LlmGateway.Infrastructure.Usage;

public sealed class EfUsageStore(IDbContextFactory<GatewayDbContext> dbFactory) : IUsageStore
{
    public async Task RecordAsync(UsageRecord record, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.UsageRecords.Add(UsageRecordEntity.FromDomain(record));
        await db.SaveChangesAsync(ct);
    }

    public async Task<UsageSummary> GetSummaryAsync(string tenantId, int take, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var records = await db.UsageRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(ct);

        var recent = records
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .Select(r => r.ToDomain())
            .ToList();

        return new UsageSummary(
            records.Count,
            records.Sum(r => r.PromptTokens),
            records.Sum(r => r.CompletionTokens),
            records.Sum(r => r.CostUsd),
            records.Count(r => r.Cached),
            recent);
    }
}
