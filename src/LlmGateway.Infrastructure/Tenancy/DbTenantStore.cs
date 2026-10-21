using LlmGateway.Core.Tenancy;
using LlmGateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LlmGateway.Infrastructure.Tenancy;

public sealed class DbTenantStore(IDbContextFactory<GatewayDbContext> dbFactory) : ITenantStore
{
    public IReadOnlyCollection<Tenant> GetAll()
    {
        using var db = dbFactory.CreateDbContext();
        var entities = db.Tenants.AsNoTracking().ToList();
        return [.. entities.Select(t => t.ToDomain())];
    }
}
