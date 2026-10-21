using LlmGateway.Core.Tenancy;

namespace LlmGateway.Api.Middleware;

public sealed class ApiKeyAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantAuthenticator authenticator, TenantContext tenantContext)
    {
        if (context.Request.Path.StartsWithSegments("/healthz")
            || context.Request.Path.StartsWithSegments("/readyz")
            || context.Request.Path.StartsWithSegments("/openapi"))
        {
            await next(context);
            return;
        }

        var header = context.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            tenantContext.Tenant = authenticator.Authenticate(header["Bearer ".Length..].Trim());
        }

        if (tenantContext.Tenant is null)
        {
            await Results.Json(
                new { error = new { message = "Missing or invalid API key. Use 'Authorization: Bearer <tenant-key>'.", type = "invalid_request_error" } },
                statusCode: StatusCodes.Status401Unauthorized)
                .ExecuteAsync(context);
            return;
        }

        context.Items[HttpContextItems.TenantId] = tenantContext.Tenant.Id;

        await next(context);
    }
}
