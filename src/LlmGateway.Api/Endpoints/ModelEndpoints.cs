using LlmGateway.Core.Privacy;
using LlmGateway.Core.Routing;
using LlmGateway.Core.Tenancy;
using LlmGateway.Core.Usage;

namespace LlmGateway.Api.Endpoints;

public sealed record GatewayModelEntry
{
    public string Id { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string? DisplayName { get; init; }
}

public static class ModelEndpoints
{
    public static void MapModelEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/models", (
            IConfiguration configuration,
            ModelRouter router) =>
        {
            var configured = configuration.GetSection("Gateway:Models").Get<GatewayModelEntry[]>() ?? [];

            var data = configured
                .Where(m => !string.IsNullOrWhiteSpace(m.Id))
                .Select(m => new
                {
                    id = m.Id,
                    @object = "model",
                    created = 0,
                    owned_by = router.Resolve(m.Id)?.Name ?? m.Provider,
                    display_name = m.DisplayName ?? m.Id,
                });

            return Results.Ok(new { @object = "list", data });
        });
    }
}
