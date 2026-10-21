using LlmGateway.Core;
using LlmGateway.Core.Routing;

namespace LlmGateway.Tests;

public class ModelRouterTests
{
    private sealed class StubProvider(string name) : ILlmProvider
    {
        public string Name => name;

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static ModelRouter CreateRouter(out ILlmProvider openai, out ILlmProvider anthropic, out ILlmProvider fake)
    {
        openai = new StubProvider("openai");
        anthropic = new StubProvider("anthropic");
        fake = new StubProvider("fake");

        return new ModelRouter(
        [
            ("gpt-*", openai),
            ("claude-*", anthropic),
            ("*", fake),
        ]);
    }

    [Fact]
    public void Routes_by_prefix_pattern()
    {
        var router = CreateRouter(out var openai, out var anthropic, out _);

        Assert.Same(openai, router.Resolve("gpt-4o-mini"));
        Assert.Same(anthropic, router.Resolve("claude-sonnet-4-5"));
    }

    [Fact]
    public void Wildcard_pattern_is_the_fallback()
    {
        var router = CreateRouter(out _, out _, out var fake);

        Assert.Same(fake, router.Resolve("llama3.2"));
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var router = CreateRouter(out var openai, out _, out _);

        Assert.Same(openai, router.Resolve("GPT-4O"));
    }

    [Fact]
    public void Exact_model_name_matches()
    {
        var provider = new StubProvider("special");
        var router = new ModelRouter([("my-exact-model", provider)]);

        Assert.Same(provider, router.Resolve("my-exact-model"));
    }

    [Fact]
    public void Throws_when_no_route_matches()
    {
        var router = new ModelRouter([("gpt-*", new StubProvider("openai"))]);

        Assert.Throws<InvalidOperationException>(() => router.Resolve("claude-x"));
    }
}
