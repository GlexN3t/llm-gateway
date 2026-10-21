using System.Collections.Concurrent;
using LlmGateway.Core.Security;

namespace LlmGateway.Core.Tenancy;

public sealed class TenantAuthenticator(ITenantStore store, ApiKeyHasher hasher)
{
    private static readonly TimeSpan PositiveCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, CacheEntry> _verified = new();

    public Tenant? Authenticate(string presentedKey)
    {
        var lookup = ApiKeyHasher.ComputeLookup(presentedKey);
        var now = DateTimeOffset.UtcNow;

        if (_verified.TryGetValue(lookup, out var cached) && cached.ExpiresAt > now)
        {
            return cached.Tenant;
        }

        var tenant = store.FindByKeyLookup(lookup) ?? FindLegacyTenant(presentedKey);

        if (tenant is null || !hasher.Verify(presentedKey, tenant.ApiKeyHash, tenant.ApiKeySalt))
        {
            _verified[lookup] = new CacheEntry(null, now + NegativeCacheTtl);
            return null;
        }

        _verified[lookup] = new CacheEntry(tenant, now + PositiveCacheTtl);
        return tenant;
    }

    private Tenant? FindLegacyTenant(string presentedKey)
    {
        foreach (var candidate in store.GetAll())
        {
            if (candidate.ApiKeyLookup is not null)
            {
                continue;
            }

            if (hasher.Verify(presentedKey, candidate.ApiKeyHash, candidate.ApiKeySalt))
            {
                return candidate;
            }
        }

        return null;
    }

    private sealed record CacheEntry(Tenant? Tenant, DateTimeOffset ExpiresAt);
}
