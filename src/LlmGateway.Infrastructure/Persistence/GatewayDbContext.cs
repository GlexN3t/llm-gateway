using Microsoft.EntityFrameworkCore;

namespace LlmGateway.Infrastructure.Persistence;

public sealed class GatewayDbContext(DbContextOptions<GatewayDbContext> options) : DbContext(options)
{
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();

    public DbSet<UsageRecordEntity> UsageRecords => Set<UsageRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantEntity>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).HasMaxLength(200);
            entity.Property(t => t.ApiKeyHash).HasMaxLength(200);
            entity.Property(t => t.ApiKeySalt).HasMaxLength(200);
            entity.Property(t => t.ApiKeyLookup).HasMaxLength(100);
            entity.HasIndex(t => t.ApiKeyLookup);
        });

        modelBuilder.Entity<UsageRecordEntity>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.TenantId, r.CreatedAt });
            entity.Property(r => r.TenantId).HasMaxLength(100);
            entity.Property(r => r.Model).HasMaxLength(200);
            entity.Property(r => r.Provider).HasMaxLength(50);
            entity.Property(r => r.CostUsd).HasPrecision(18, 8);
        });
    }
}
