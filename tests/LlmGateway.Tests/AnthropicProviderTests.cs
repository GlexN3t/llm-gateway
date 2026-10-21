using System.Net;
using System.Text;
using LlmGateway.Core;
using LlmGateway.Core.Providers;
using LlmGateway.Tests.TestDoubles;

namespace LlmGateway.Tests;

public class AnthropicProviderTests
{
    private static StubHttpHandler OkHandler(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    private static AnthropicProvider CreateProvider(StubHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.test") });

    [Fact]
    public async Task Maps_response_and_joins_text_blocks()
    {
        var handler = OkHandler("""
        {
          "id": "msg_1",
          "model": "claude-sonnet-4-5",
          "content": [
            { "type": "text", "text": "Hi " },
            { "type": "text", "text": "there" }
          ],
          "usage": { "input_tokens": 7, "output_tokens": 3 }
        }
        """);

        var provider = CreateProvider(handler);
        var result = await provider.CompleteAsync(new ChatRequest
        {
            Model = "claude-sonnet-4-5",
            Messages = [new ChatMessage("user", "hi")],
        });

        Assert.Equal("Hi there", result.Content);
        Assert.Equal("anthropic", result.Provider);
        Assert.Equal(10, result.Usage.TotalTokens);
    }

    [Fact]
    public async Task Moves_system_messages_to_top_level_field()
    {
        var handler = OkHandler("""
        {
          "id": "msg_2",
          "model": "claude-sonnet-4-5",
          "content": [ { "type": "text", "text": "ok" } ],
          "usage": { "input_tokens": 1, "output_tokens": 1 }
        }
        """);

        var provider = CreateProvider(handler);
        await provider.CompleteAsync(new ChatRequest
        {
            Model = "claude-sonnet-4-5",
            Messages =
            [
                new ChatMessage("system", "You are helpful"),
                new ChatMessage("user", "hello"),
            ],
        });

        Assert.Equal("/v1/messages", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.True(handler.LastRequest.Headers.TryGetValues("anthropic-version", out _));

        var body = handler.LastRequestBody;
        Assert.Contains("\"system\":\"You are helpful\"", body);
        Assert.DoesNotContain("\"role\":\"system\"", body);
        Assert.Contains("\"role\":\"user\"", body);
    }
}
