using LlmGateway.Core;
using LlmGateway.Core.Providers;

namespace LlmGateway.Tests;

public class FakeProviderTests
{
    [Fact]
    public async Task Echoes_last_user_message_deterministically()
    {
        var provider = new FakeProvider();

        var result = await provider.CompleteAsync(new ChatRequest
        {
            Model = "anything",
            Messages =
            [
                new ChatMessage("system", "be nice"),
                new ChatMessage("user", "hello gateway"),
            ],
        });

        Assert.Equal("fake", result.Provider);
        Assert.Equal("anything", result.Model);
        Assert.Contains("hello gateway", result.Content);
        Assert.True(result.Usage.TotalTokens > 0);
    }

    [Fact]
    public async Task Works_with_empty_messages()
    {
        var provider = new FakeProvider();

        var result = await provider.CompleteAsync(new ChatRequest
        {
            Model = "anything",
            Messages = [],
        });

        Assert.Contains("[fake:anything]", result.Content);
        Assert.True(result.Usage.TotalTokens > 0);
    }
}
