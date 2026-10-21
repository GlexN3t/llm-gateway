using LlmGateway.Core;
using LlmGateway.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace LlmGateway.Tests;

public class MemoryChatCacheTests : IDisposable
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions { SizeLimit = 100 });
    private readonly MemoryChatCache _cache;

    public MemoryChatCacheTests() => _cache = new MemoryChatCache(_memoryCache, TimeSpan.FromMinutes(5));

    public void Dispose() => _memoryCache.Dispose();

    private static ChatResponse Response() => new()
    {
        Id = "id",
        Model = "demo-model",
        Provider = "fake",
        Content = "hi",
        Usage = new TokenUsage(1, 2),
    };

    [Fact]
    public void Set_then_get_returns_same_response()
    {
        _cache.Set("key", Response());

        Assert.True(_cache.TryGet("key", out var cached));
        Assert.Equal("hi", cached!.Content);
    }

    [Fact]
    public void Missing_key_returns_false()
    {
        Assert.False(_cache.TryGet("nope", out var cached));
        Assert.Null(cached);
    }

    [Fact]
    public void Expired_entry_is_not_returned()
    {
        var cache = new MemoryChatCache(_memoryCache, TimeSpan.FromMilliseconds(20));
        cache.Set("key", Response());
        Thread.Sleep(80);

        Assert.False(cache.TryGet("key", out _));
    }
}
