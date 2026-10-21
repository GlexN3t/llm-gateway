using LlmGateway.Core.Security;
using LlmGateway.Core.Tenancy;
using LlmGateway.Infrastructure.Tenancy;

namespace LlmGateway.Tests;

public class InMemoryTenantStoreTests
{
    [Fact]
    public void Seeded_key_authenticates_against_store()
    {
        var hasher = new ApiKeyHasher();
        var store = new InMemoryTenantStore(hasher, "acme", "Acme Inc", "gw-acme-key");
        var authenticator = new TenantAuthenticator(store, hasher);

        var tenant = authenticator.Authenticate("gw-acme-key");

        Assert.NotNull(tenant);
        Assert.Equal("acme", tenant.Id);
    }

    [Fact]
    public void Generated_key_has_expected_prefix_and_length()
    {
        var key = InMemoryTenantStore.GenerateKey();

        Assert.StartsWith("gw-", key);
        Assert.True(key.Length > 32);
    }
}
