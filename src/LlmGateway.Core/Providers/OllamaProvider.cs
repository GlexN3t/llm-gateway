using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LlmGateway.Core.Providers;

public sealed class OllamaProvider : ILlmProvider
{
    private readonly HttpClient _http;

    public OllamaProvider(HttpClient http)
    {
        _http = http;
    }

    public string Name => "ollama";

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
    {
        var payload = new OllamaRequest(
            request.Model,
            [.. request.Messages.Select(m => new OllamaMessage(m.Role, m.Content))],
            Stream: false,
            Options: request.Temperature is null ? null : new OllamaOptions(request.Temperature.Value));

        using var response = await _http.PostAsJsonAsync("/api/chat", payload, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OllamaResponse>(ct)
            ?? throw new InvalidOperationException("Empty response from Ollama.");

        return new ChatResponse
        {
            Id = $"ollama-{Guid.NewGuid():N}",
            Model = body.Model,
            Provider = Name,
            Content = body.Message.Content,
            Usage = new TokenUsage(body.PromptEvalCount, body.EvalCount),
        };
    }

    private sealed record OllamaRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OllamaOptions? Options);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaOptions(
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record OllamaResponse(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("message")] OllamaMessage Message,
        [property: JsonPropertyName("prompt_eval_count")] int PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int EvalCount);
}
