namespace LlmGateway.Core.Tenancy;

public interface ITenantStore
{
    IReadOnlyCollection<Tenant> GetAll();
}
