namespace LlmGateway.Core.Tenancy;

public sealed class TenantContext
{
    public Tenant? Tenant { get; set; }

    public Tenant RequireTenant() =>
        Tenant ?? throw new InvalidOperationException("Tenant was not authenticated for this request.");
}
