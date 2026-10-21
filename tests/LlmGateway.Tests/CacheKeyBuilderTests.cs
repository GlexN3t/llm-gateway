using LlmGateway.Core;
using LlmGateway.Core.Caching;

namespace LlmGateway.Tests;

public class CacheKeyBuilderTests
{
    private static ChatRequest Request(string model = "demo-model", string content = "hello") => new()
    {
        Model = model,
        Messages = [new ChatMessage("user", content)],
        Temperature = 0.7f,
        MaxTokens = 128,
    };

    [Fact]
    public void Same_request_same_key()
    {
        Assert.Equal(
            CacheKeyBuilder.Build("acme", Request()),
            CacheKeyBuilder.Build("acme", Request()));
    }

    [Fact]
    public void Different_tenants_get_different_keys()
    {
        Assert.NotEqual(
            CacheKeyBuilder.Build("acme", Request()),
            CacheKeyBuilder.Build("other", Request()));
    }

    [Fact]
    public void Different_content_gets_different_key()
    {
        Assert.NotEqual(
            CacheKeyBuilder.Build("acme", Request(content: "hello")),
            CacheKeyBuilder.Build("acme", Request(content: "goodbye")));
    }

    [Fact]
    public void Different_model_gets_different_key()
    {
        Assert.NotEqual(
            CacheKeyBuilder.Build("acme", Request(model: "model-a")),
            CacheKeyBuilder.Build("acme", Request(model: "model-b")));
    }
}
