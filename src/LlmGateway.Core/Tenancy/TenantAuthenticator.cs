using LlmGateway.Core.Security;

namespace LlmGateway.Core.Tenancy;

public sealed class TenantAuthenticator(ITenantStore store, ApiKeyHasher hasher)
{
    public Tenant? Authenticate(string presentedKey)
    {
        foreach (var tenant in store.GetAll())
        {
            if (hasher.Verify(presentedKey, tenant.ApiKeyHash, tenant.ApiKeySalt))
            {
                return tenant;
            }
        }

        return null;
    }
}
