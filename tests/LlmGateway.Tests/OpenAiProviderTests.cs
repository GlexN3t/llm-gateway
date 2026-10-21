using System.Net;
using System.Text;
using LlmGateway.Core;
using LlmGateway.Core.Providers;
using LlmGateway.Tests.TestDoubles;

namespace LlmGateway.Tests;

public class OpenAiProviderTests
{
    private static StubHttpHandler OkHandler(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    private static OpenAiProvider CreateProvider(StubHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.test") });

    [Fact]
    public async Task Maps_response_to_unified_shape()
    {
        var handler = OkHandler("""
        {
          "id": "chatcmpl-1",
          "model": "gpt-4o-mini",
          "choices": [ { "message": { "role": "assistant", "content": "Hello!" } } ],
          "usage": { "prompt_tokens": 10, "completion_tokens": 5 }
        }
        """);

        var provider = CreateProvider(handler);
        var result = await provider.CompleteAsync(new ChatRequest
        {
            Model = "gpt-4o-mini",
            Messages = [new ChatMessage("user", "hi")],
        });

        Assert.Equal("chatcmpl-1", result.Id);
        Assert.Equal("gpt-4o-mini", result.Model);
        Assert.Equal("openai", result.Provider);
        Assert.Equal("Hello!", result.Content);
        Assert.Equal(15, result.Usage.TotalTokens);
    }

    [Fact]
    public async Task Sends_request_to_correct_endpoint_with_model()
    {
        var handler = OkHandler("""
        {
          "id": "1",
          "model": "gpt-4o-mini",
          "choices": [ { "message": { "role": "assistant", "content": "ok" } } ],
          "usage": { "prompt_tokens": 1, "completion_tokens": 1 }
        }
        """);

        var provider = CreateProvider(handler);
        await provider.CompleteAsync(new ChatRequest
        {
            Model = "gpt-4o-mini",
            Messages = [new ChatMessage("user", "hello")],
            Temperature = 0.2,
        });

        Assert.Equal("/v1/chat/completions", handler.LastRequest!.RequestUri!.AbsolutePath);

        var body = await handler.LastRequest.Content!.ReadAsStringAsync();
        Assert.Contains("\"model\":\"gpt-4o-mini\"", body);
        Assert.Contains("\"role\":\"user\"", body);
        Assert.Contains("\"temperature\":0.2", body);
    }
}
