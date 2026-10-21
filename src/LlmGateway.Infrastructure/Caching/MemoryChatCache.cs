using LlmGateway.Core;
using LlmGateway.Core.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace LlmGateway.Infrastructure.Caching;

public sealed class MemoryChatCache(IMemoryCache cache, TimeSpan ttl) : IChatCache
{
    public bool TryGet(string key, out ChatResponse? response) => cache.TryGetValue(key, out response);

    public void Set(string key, ChatResponse response) =>
        cache.Set(key, response, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1,
        });
}
