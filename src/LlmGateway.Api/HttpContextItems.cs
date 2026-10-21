namespace LlmGateway.Api;

public static class HttpContextItems
{
    public const string RequestId = "RequestId";
    public const string TenantId = "TenantId";
}

public static class RateLimiting
{
    public const string PolicyName = "per-tenant";
}

public sealed record GatewayRoute
{
    public string Pattern { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;
}
