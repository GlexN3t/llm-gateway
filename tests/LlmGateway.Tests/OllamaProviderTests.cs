using System.Net;
using System.Text;
using LlmGateway.Core;
using LlmGateway.Core.Providers;
using LlmGateway.Tests.TestDoubles;

namespace LlmGateway.Tests;

public class OllamaProviderTests
{
    private static StubHttpHandler OkHandler(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    private static OllamaProvider CreateProvider(StubHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") });

    [Fact]
    public async Task Maps_response_to_unified_shape()
    {
        var handler = OkHandler("""
        {
          "model": "llama3.2",
          "message": { "role": "assistant", "content": "Hello from Ollama" },
          "prompt_eval_count": 12,
          "eval_count": 4
        }
        """);

        var provider = CreateProvider(handler);
        var result = await provider.CompleteAsync(new ChatRequest
        {
            Model = "llama3.2",
            Messages = [new ChatMessage("user", "hi")],
        });

        Assert.Equal("llama3.2", result.Model);
        Assert.Equal("ollama", result.Provider);
        Assert.Equal("Hello from Ollama", result.Content);
        Assert.Equal(16, result.Usage.TotalTokens);
    }

    [Fact]
    public async Task Disables_streaming_in_request()
    {
        var handler = OkHandler("""
        {
          "model": "llama3.2",
          "message": { "role": "assistant", "content": "ok" },
          "prompt_eval_count": 1,
          "eval_count": 1
        }
        """);

        var provider = CreateProvider(handler);
        await provider.CompleteAsync(new ChatRequest
        {
            Model = "llama3.2",
            Messages = [new ChatMessage("user", "hello")],
        });

        Assert.Equal("/api/chat", handler.LastRequest!.RequestUri!.AbsolutePath);

        var body = await handler.LastRequest.Content!.ReadAsStringAsync();
        Assert.Contains("\"stream\":false", body);
    }
}
