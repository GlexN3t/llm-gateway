using LlmGateway.Core.Security;
using LlmGateway.Core.Tenancy;

namespace LlmGateway.Tests;

public class TenantAuthenticatorTests
{
    private sealed class ListStore(params Tenant[] tenants) : ITenantStore
    {
        public IReadOnlyCollection<Tenant> GetAll() => tenants;
    }

    [Fact]
    public void Resolves_tenant_from_valid_key()
    {
        var hasher = new ApiKeyHasher();
        var (hash, salt) = hasher.Hash("gw-alpha-key");
        var store = new ListStore(new Tenant("alpha", "Alpha Inc", hash, salt));
        var authenticator = new TenantAuthenticator(store, hasher);

        var tenant = authenticator.Authenticate("gw-alpha-key");

        Assert.NotNull(tenant);
        Assert.Equal("alpha", tenant.Id);
    }

    [Fact]
    public void Returns_null_for_invalid_key()
    {
        var hasher = new ApiKeyHasher();
        var (hash, salt) = hasher.Hash("gw-alpha-key");
        var store = new ListStore(new Tenant("alpha", "Alpha Inc", hash, salt));
        var authenticator = new TenantAuthenticator(store, hasher);

        Assert.Null(authenticator.Authenticate("gw-wrong-key"));
    }

    [Fact]
    public void Finds_the_right_tenant_among_many()
    {
        var hasher = new ApiKeyHasher();
        var alpha = hasher.Hash("gw-alpha-key");
        var beta = hasher.Hash("gw-beta-key");
        var store = new ListStore(
            new Tenant("alpha", "Alpha Inc", alpha.Hash, alpha.Salt),
            new Tenant("beta", "Beta LLC", beta.Hash, beta.Salt));
        var authenticator = new TenantAuthenticator(store, hasher);

        var tenant = authenticator.Authenticate("gw-beta-key");

        Assert.NotNull(tenant);
        Assert.Equal("beta", tenant.Id);
    }
}
