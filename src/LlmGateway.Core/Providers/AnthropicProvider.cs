using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LlmGateway.Core.Providers;

public sealed class AnthropicProvider : ILlmProvider
{
    private readonly HttpClient _http;

    public AnthropicProvider(HttpClient http)
    {
        http.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        _http = http;
    }

    public string Name => "anthropic";

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
    {
        var system = string.Join("\n\n", request.Messages.Where(m => m.Role == "system").Select(m => m.Content));
        var messages = request.Messages
            .Where(m => m.Role != "system")
            .Select(m => new AnthropicMessage(m.Role, m.Content))
            .ToList();

        var payload = new AnthropicRequest(
            request.Model,
            request.MaxTokens ?? 1024,
            string.IsNullOrEmpty(system) ? null : system,
            messages,
            request.Temperature);

        using var response = await _http.PostAsJsonAsync("/v1/messages", payload, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AnthropicResponse>(ct)
            ?? throw new InvalidOperationException("Empty response from Anthropic.");

        var text = string.Concat(body.Content.Where(c => c.Type == "text").Select(c => c.Text));

        return new ChatResponse
        {
            Id = body.Id,
            Model = body.Model,
            Provider = Name,
            Content = text,
            Usage = new TokenUsage(body.Usage.InputTokens, body.Usage.OutputTokens),
        };
    }

    private sealed record AnthropicRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? System,
        [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages,
        [property: JsonPropertyName("temperature"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Temperature);

    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record AnthropicResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("content")] IReadOnlyList<AnthropicContent> Content,
        [property: JsonPropertyName("usage")] AnthropicUsage Usage);

    private sealed record AnthropicContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);

    private sealed record AnthropicUsage(
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens);
}
