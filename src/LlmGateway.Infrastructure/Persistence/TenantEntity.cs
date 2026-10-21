using LlmGateway.Core.Tenancy;

namespace LlmGateway.Infrastructure.Persistence;

public sealed class TenantEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ApiKeyHash { get; set; } = string.Empty;
    public string ApiKeySalt { get; set; } = string.Empty;
    public bool PiiRedactionEnabled { get; set; } = true;

    public Tenant ToDomain() => new(Id, Name, ApiKeyHash, ApiKeySalt, PiiRedactionEnabled);
}
