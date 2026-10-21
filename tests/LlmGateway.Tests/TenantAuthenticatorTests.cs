using LlmGateway.Core.Security;
using LlmGateway.Core.Tenancy;

namespace LlmGateway.Tests;

public class TenantAuthenticatorTests
{
    private sealed class ListStore(params Tenant[] tenants) : ITenantStore
    {
        public int GetAllCalls { get; private set; }

        public int LookupCalls { get; private set; }

        public IReadOnlyCollection<Tenant> GetAll()
        {
            GetAllCalls++;
            return tenants;
        }

        public Tenant? FindByKeyLookup(string lookup)
        {
            LookupCalls++;
            return tenants.FirstOrDefault(t => t.ApiKeyLookup == lookup);
        }
    }

    private static (Tenant Tenant, string Key) TenantWithKey(ApiKeyHasher hasher, string id, string key)
    {
        var (hash, salt) = hasher.Hash(key);
        return (new Tenant(id, $"{id} Inc", hash, salt, ApiKeyLookup: ApiKeyHasher.ComputeLookup(key)), key);
    }

    [Fact]
    public void Resolves_tenant_from_valid_key()
    {
        var hasher = new ApiKeyHasher();
        var (tenant, key) = TenantWithKey(hasher, "alpha", "gw-alpha-key");
        var store = new ListStore(tenant);
        var authenticator = new TenantAuthenticator(store, hasher);

        var resolved = authenticator.Authenticate(key);

        Assert.NotNull(resolved);
        Assert.Equal("alpha", resolved.Id);
    }

    [Fact]
    public void Returns_null_for_invalid_key()
    {
        var hasher = new ApiKeyHasher();
        var (tenant, _) = TenantWithKey(hasher, "alpha", "gw-alpha-key");
        var store = new ListStore(tenant);
        var authenticator = new TenantAuthenticator(store, hasher);

        Assert.Null(authenticator.Authenticate("gw-wrong-key"));
    }

    [Fact]
    public void Finds_the_right_tenant_among_many()
    {
        var hasher = new ApiKeyHasher();
        var (alpha, alphaKey) = TenantWithKey(hasher, "alpha", "gw-alpha-key");
        var (beta, betaKey) = TenantWithKey(hasher, "beta", "gw-beta-key");
        var store = new ListStore(alpha, beta);
        var authenticator = new TenantAuthenticator(store, hasher);

        var resolved = authenticator.Authenticate(betaKey);

        Assert.NotNull(resolved);
        Assert.Equal("beta", resolved.Id);
    }

    [Fact]
    public void Uses_lookup_index_instead_of_scanning_every_tenant()
    {
        var hasher = new ApiKeyHasher();
        var (alpha, alphaKey) = TenantWithKey(hasher, "alpha", "gw-alpha-key");
        var (beta, _) = TenantWithKey(hasher, "beta", "gw-beta-key");
        var store = new ListStore(alpha, beta);
        var authenticator = new TenantAuthenticator(store, hasher);

        authenticator.Authenticate(alphaKey);

        Assert.Equal(1, store.LookupCalls);
        Assert.Equal(0, store.GetAllCalls);
    }

    [Fact]
    public void Caches_successful_verification()
    {
        var hasher = new ApiKeyHasher();
        var (alpha, alphaKey) = TenantWithKey(hasher, "alpha", "gw-alpha-key");
        var store = new ListStore(alpha);
        var authenticator = new TenantAuthenticator(store, hasher);

        var first = authenticator.Authenticate(alphaKey);
        var second = authenticator.Authenticate(alphaKey);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, store.LookupCalls);
    }

    [Fact]
    public void Supports_legacy_tenants_without_lookup_index()
    {
        var hasher = new ApiKeyHasher();
        var (hash, salt) = hasher.Hash("gw-legacy-key");
        var legacy = new Tenant("legacy", "Legacy Inc", hash, salt);
        var store = new ListStore(legacy);
        var authenticator = new TenantAuthenticator(store, hasher);

        var resolved = authenticator.Authenticate("gw-legacy-key");

        Assert.NotNull(resolved);
        Assert.Equal("legacy", resolved.Id);
    }
}
