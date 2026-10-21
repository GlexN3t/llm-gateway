using LlmGateway.Core.Usage;
using LlmGateway.Infrastructure.Persistence;
using LlmGateway.Infrastructure.Usage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LlmGateway.Tests;

public class EfUsageStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PooledDbContextFactory<GatewayDbContext> _factory;

    public EfUsageStoreTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<GatewayDbContext>().UseSqlite(_connection).Options;
        _factory = new PooledDbContextFactory<GatewayDbContext>(options);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private static UsageRecord Record(string tenant, string model, int prompt, int completion, decimal cost) =>
        new(0, tenant, model, "fake", prompt, completion, cost, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Records_persist_and_summarize()
    {
        var store = new EfUsageStore(_factory);
        await store.RecordAsync(Record("acme", "demo", 10, 5, 0.001m));
        await store.RecordAsync(Record("acme", "demo", 20, 5, 0.002m));

        var summary = await store.GetSummaryAsync("acme", 10);

        Assert.Equal(2, summary.RequestCount);
        Assert.Equal(30, summary.PromptTokens);
        Assert.Equal(10, summary.CompletionTokens);
        Assert.Equal(0.003m, summary.TotalCostUsd);
        Assert.Equal(2, summary.RecentRecords.Count);
    }

    [Fact]
    public async Task Tenant_summaries_are_isolated()
    {
        var store = new EfUsageStore(_factory);
        await store.RecordAsync(Record("acme", "demo", 10, 5, 0.001m));
        await store.RecordAsync(Record("other", "demo", 99, 99, 9m));

        var summary = await store.GetSummaryAsync("acme", 10);

        Assert.Equal(1, summary.RequestCount);
        Assert.Equal(15, summary.TotalTokens);
    }

    [Fact]
    public async Task Counts_cached_requests_separately()
    {
        var store = new EfUsageStore(_factory);
        await store.RecordAsync(Record("acme", "demo", 10, 5, 0m) with { Cached = true });
        await store.RecordAsync(Record("acme", "demo", 10, 5, 0.001m));

        var summary = await store.GetSummaryAsync("acme", 10);

        Assert.Equal(2, summary.RequestCount);
        Assert.Equal(1, summary.CachedRequestCount);
    }

    [Fact]
    public async Task Recent_records_are_limited_by_take()
    {
        var store = new EfUsageStore(_factory);
        for (var i = 0; i < 5; i++)
        {
            await store.RecordAsync(Record("acme", "demo", i + 1, 1, 0m));
        }

        var summary = await store.GetSummaryAsync("acme", 3);

        Assert.Equal(5, summary.RequestCount);
        Assert.Equal(3, summary.RecentRecords.Count);
    }
}
