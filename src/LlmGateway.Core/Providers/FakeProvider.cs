namespace LlmGateway.Core.Providers;

public sealed class FakeProvider : ILlmProvider
{
    public string Name => "fake";

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
    {
        var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;
        var content = $"[fake:{request.Model}] You said: {lastUserMessage}";

        var promptTokens = request.Messages.Sum(m => EstimateTokens(m.Content));
        var completionTokens = EstimateTokens(content);

        var response = new ChatResponse
        {
            Id = $"fake-{Guid.NewGuid():N}",
            Model = request.Model,
            Provider = Name,
            Content = content,
            Usage = new TokenUsage(promptTokens, completionTokens),
        };

        return Task.FromResult(response);
    }

    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);
}
