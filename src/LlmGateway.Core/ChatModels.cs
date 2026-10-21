namespace LlmGateway.Core;

public sealed record ChatMessage(string Role, string Content);

public sealed record ChatRequest
{
    public required string Model { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
}

public sealed record TokenUsage(int PromptTokens, int CompletionTokens)
{
    public int TotalTokens => PromptTokens + CompletionTokens;
}

public sealed record ChatResponse
{
    public required string Id { get; init; }
    public required string Model { get; init; }
    public required string Provider { get; init; }
    public required string Content { get; init; }
    public required TokenUsage Usage { get; init; }
}
