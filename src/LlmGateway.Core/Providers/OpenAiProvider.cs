using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LlmGateway.Core.Providers;

public sealed class OpenAiProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly ProviderTransportOptions _options;

    public OpenAiProvider(HttpClient http, ProviderTransportOptions? options = null)
    {
        _http = http;
        _options = options ?? new ProviderTransportOptions();
    }

    public string Name => "openai";

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
    {
        var payload = new OpenAiRequest(
            request.Model,
            [.. request.Messages.Select(m => new OpenAiMessage(m.Role, m.Content))],
            request.Temperature,
            request.MaxTokens);

        using var response = await ProviderTransport.SendAsync(
            _http,
            () => new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
            {
                Content = JsonContent.Create(payload),
            },
            Name,
            _options,
            ct);

        var body = await response.Content.ReadFromJsonAsync<OpenAiResponse>(ct)
            ?? throw new ProviderException(Name, ProviderErrorKind.InvalidResponse, "openai returned an empty body.");

        if (body.Choices.Count == 0)
        {
            throw new ProviderException(Name, ProviderErrorKind.InvalidResponse, "openai returned no completion choices.");
        }

        return new ChatResponse
        {
            Id = body.Id,
            Model = body.Model,
            Provider = Name,
            Content = body.Choices[0].Message.Content,
            Usage = new TokenUsage(body.Usage.PromptTokens, body.Usage.CompletionTokens),
        };
    }

    private sealed record OpenAiRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiMessage> Messages,
        [property: JsonPropertyName("temperature"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Temperature,
        [property: JsonPropertyName("max_tokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxTokens);

    private sealed record OpenAiMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenAiResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("choices")] IReadOnlyList<OpenAiChoice> Choices,
        [property: JsonPropertyName("usage")] OpenAiUsage Usage);

    private sealed record OpenAiChoice(
        [property: JsonPropertyName("message")] OpenAiMessage Message);

    private sealed record OpenAiUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);
}
