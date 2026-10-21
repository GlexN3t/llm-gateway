namespace LlmGateway.Core.Tenancy;

public interface ITenantStore
{
    IReadOnlyCollection<Tenant> GetAll();

    Tenant? FindByKeyLookup(string lookup);
}
