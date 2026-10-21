using LlmGateway.Infrastructure.Persistence;
using LlmGateway.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LlmGateway.Tests;

public class DbTenantStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PooledDbContextFactory<GatewayDbContext> _factory;

    public DbTenantStoreTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<GatewayDbContext>().UseSqlite(_connection).Options;
        _factory = new PooledDbContextFactory<GatewayDbContext>(options);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Returns_all_tenants_as_domain_records()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Tenants.Add(new TenantEntity { Id = "acme", Name = "Acme Inc", ApiKeyHash = "hash", ApiKeySalt = "salt" });
            db.SaveChanges();
        }

        var store = new DbTenantStore(_factory);
        var tenants = store.GetAll();

        var tenant = Assert.Single(tenants);
        Assert.Equal("acme", tenant.Id);
        Assert.Equal("Acme Inc", tenant.Name);
    }

    [Fact]
    public void Returns_empty_when_no_tenants()
    {
        var store = new DbTenantStore(_factory);

        Assert.Empty(store.GetAll());
    }
}
