using LlmGateway.Core.Tenancy;
using LlmGateway.Core.Usage;

namespace LlmGateway.Api.Endpoints;

public static class UsageEndpoints
{
    public static void MapUsageEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/usage", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        IUsageStore usageStore,
        TenantContext tenantContext,
        CancellationToken ct,
        int take = 20)
    {
        var tenant = tenantContext.RequireTenant();
        var summary = await usageStore.GetSummaryAsync(tenant.Id, Math.Clamp(take, 1, 100), ct);
        return Results.Ok(summary);
    }
}
