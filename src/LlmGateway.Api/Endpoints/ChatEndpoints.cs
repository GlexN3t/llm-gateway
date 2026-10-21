using LlmGateway.Core;
using LlmGateway.Core.Caching;
using LlmGateway.Core.Privacy;
using LlmGateway.Core.Routing;
using LlmGateway.Core.Tenancy;
using LlmGateway.Core.Usage;

namespace LlmGateway.Api.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/chat/completions", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        ChatCompletionRequest? request,
        HttpContext httpContext,
        ModelRouter router,
        TenantContext tenantContext,
        CostCalculator costCalculator,
        IUsageStore usageStore,
        PiiRedactor piiRedactor,
        IChatCache cache,
        CancellationToken ct)
    {
        var tenant = tenantContext.RequireTenant();

        if (request is null || string.IsNullOrWhiteSpace(request.Model))
        {
            return Results.BadRequest(new { error = "model is required" });
        }

        if (request.Messages is not { Count: > 0 })
        {
            return Results.BadRequest(new { error = "messages must contain at least one item" });
        }

        var messages = request.Messages
            .Where(m => !string.IsNullOrEmpty(m.Role) && !string.IsNullOrEmpty(m.Content))
            .Select(m => new ChatMessage(m.Role!, m.Content!))
            .ToList();

        if (messages.Count == 0)
        {
            return Results.BadRequest(new { error = "messages must have role and content" });
        }

        var provider = router.Resolve(request.Model);
        if (provider is null)
        {
            return Results.BadRequest(new { error = $"no provider configured for model '{request.Model}'" });
        }

        IReadOnlyList<ChatMessage> outboundMessages = messages;
        IReadOnlyDictionary<string, string>? redactionMap = null;

        if (tenant.PiiRedactionEnabled)
        {
            var redaction = piiRedactor.Redact([.. messages.Select(m => m.Content)]);
            if (redaction.Map.Count > 0)
            {
                outboundMessages = [.. messages.Select((m, i) => m with { Content = redaction.Texts[i] })];
                redactionMap = redaction.Map;
            }
        }

        var chatRequest = new ChatRequest
        {
            Model = request.Model,
            Messages = outboundMessages,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
        };

        var cacheKey = CacheKeyBuilder.Build(tenant.Id, chatRequest);
        var fromCache = cache.TryGet(cacheKey, out var cachedResponse) && cachedResponse is not null;

        ChatResponse response;
        if (fromCache)
        {
            response = cachedResponse!;
        }
        else
        {
            response = await provider.CompleteAsync(chatRequest, ct);
            cache.Set(cacheKey, response);
        }

        httpContext.Response.Headers["X-Cache"] = fromCache ? "HIT" : "MISS";

        if (redactionMap is { Count: > 0 })
        {
            response = response with { Content = piiRedactor.Restore(response.Content, redactionMap) };
        }

        await usageStore.RecordAsync(new UsageRecord(
            Id: 0,
            TenantId: tenant.Id,
            Model: response.Model,
            Provider: response.Provider,
            PromptTokens: response.Usage.PromptTokens,
            CompletionTokens: response.Usage.CompletionTokens,
            CostUsd: fromCache ? 0m : costCalculator.Calculate(response.Model, response.Usage),
            CreatedAt: DateTimeOffset.UtcNow,
            Cached: fromCache), ct);

        return Results.Ok(ChatCompletionResponse.From(response));
    }
}

public sealed class ChatCompletionRequest
{
    public string? Model { get; init; }
    public List<ChatCompletionMessage>? Messages { get; init; }
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
}

public sealed class ChatCompletionMessage
{
    public string? Role { get; init; }
    public string? Content { get; init; }
}

public sealed record ChatCompletionChoice(int Index, ChatCompletionMessage Message, string FinishReason);

public sealed record ChatCompletionUsage(int PromptTokens, int CompletionTokens, int TotalTokens);

public sealed record ChatCompletionResponse(
    string Id,
    string Object,
    long Created,
    string Model,
    string Provider,
    IReadOnlyList<ChatCompletionChoice> Choices,
    ChatCompletionUsage Usage)
{
    public static ChatCompletionResponse From(ChatResponse response) => new(
        response.Id,
        "chat.completion",
        DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        response.Model,
        response.Provider,
        [new ChatCompletionChoice(0, new ChatCompletionMessage { Role = "assistant", Content = response.Content }, "stop")],
        new ChatCompletionUsage(
            response.Usage.PromptTokens,
            response.Usage.CompletionTokens,
            response.Usage.PromptTokens + response.Usage.CompletionTokens));
}
