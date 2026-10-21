namespace LlmGateway.Core.Tenancy;

public sealed record Tenant(
    string Id,
    string Name,
    string ApiKeyHash,
    string ApiKeySalt,
    bool PiiRedactionEnabled = true,
    string? ApiKeyLookup = null);
