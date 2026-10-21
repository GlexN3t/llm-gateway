using System.Security.Cryptography;
using LlmGateway.Core.Security;
using LlmGateway.Core.Tenancy;

namespace LlmGateway.Infrastructure.Tenancy;

public sealed class InMemoryTenantStore : ITenantStore
{
    private readonly List<Tenant> _tenants;

    public InMemoryTenantStore(ApiKeyHasher hasher, string tenantId, string tenantName, string apiKey)
    {
        var (hash, salt) = hasher.Hash(apiKey);
        _tenants = [new Tenant(tenantId, tenantName, hash, salt)];
    }

    public IReadOnlyCollection<Tenant> GetAll() => _tenants;

    public static string GenerateKey() =>
        $"gw-{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
}
