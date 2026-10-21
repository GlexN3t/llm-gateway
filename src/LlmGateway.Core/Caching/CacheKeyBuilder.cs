using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LlmGateway.Core.Caching;

public static class CacheKeyBuilder
{
    public static string Build(string tenantId, ChatRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            tenantId,
            model = request.Model,
            messages = request.Messages.Select(m => new { m.Role, m.Content }),
            request.Temperature,
            request.MaxTokens,
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
