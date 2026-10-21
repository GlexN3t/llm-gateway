namespace LlmGateway.Core;

public interface ILlmProvider
{
    string Name { get; }

    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default);
}
