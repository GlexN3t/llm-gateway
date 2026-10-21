using System.Net;
using System.Text;
using LlmGateway.Core;
using LlmGateway.Core.Providers;
using LlmGateway.Tests.TestDoubles;

namespace LlmGateway.Tests;

public class ProviderResilienceTests
{
    private static readonly ProviderTransportOptions FastOptions = new(
        MaxAttempts: 3,
        Timeout: TimeSpan.FromSeconds(5),
        BaseDelay: TimeSpan.Zero);

    private static OpenAiProvider CreateProvider(HttpMessageHandler handler, ProviderTransportOptions? options = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.test") }, options ?? FastOptions);

    private static HttpResponseMessage Json(HttpStatusCode status, string body, TimeSpan? retryAfter = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (retryAfter is { } delta)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delta);
        }

        return response;
    }

    private static ChatRequest Request() => new()
    {
        Model = "gpt-4o-mini",
        Messages = [new ChatMessage("user", "hi")],
    };

    [Fact]
    public async Task Upstream_server_error_surfaces_as_provider_exception()
    {
        var handler = new StubHttpHandler((_, _) => Json(HttpStatusCode.InternalServerError, "{}"));

        var provider = CreateProvider(handler, FastOptions with { MaxAttempts = 1 });

        var ex = await Assert.ThrowsAsync<ProviderException>(() => provider.CompleteAsync(Request()));

        Assert.Equal("openai", ex.Provider);
        Assert.Equal(ProviderErrorKind.Upstream, ex.Kind);
        Assert.Equal(500, ex.UpstreamStatusCode);
        Assert.True(ex.Retryable);
    }

    [Fact]
    public async Task Rate_limited_upstream_keeps_retry_after()
    {
        var handler = new StubHttpHandler((_, _) => Json(HttpStatusCode.TooManyRequests, "{}", TimeSpan.FromSeconds(7)));

        var provider = CreateProvider(handler, FastOptions with { MaxAttempts = 1 });

        var ex = await Assert.ThrowsAsync<ProviderException>(() => provider.CompleteAsync(Request()));

        Assert.Equal(ProviderErrorKind.Upstream, ex.Kind);
        Assert.Equal(429, ex.UpstreamStatusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), ex.RetryAfter);
    }

    [Fact]
    public async Task Transient_failure_is_retried_then_succeeds()
    {
        var handler = new StubHttpHandler((_, attempt) => attempt < 3
            ? Json(HttpStatusCode.BadGateway, "{}")
            : Json(HttpStatusCode.OK, """
              {
                "id": "chatcmpl-1",
                "model": "gpt-4o-mini",
                "choices": [ { "message": { "role": "assistant", "content": "recovered" } } ],
                "usage": { "prompt_tokens": 3, "completion_tokens": 4 }
              }
              """));

        var provider = CreateProvider(handler);

        var result = await provider.CompleteAsync(Request());

        Assert.Equal("recovered", result.Content);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task Client_error_is_not_retried()
    {
        var handler = new StubHttpHandler((_, _) => Json(HttpStatusCode.BadRequest, "{}"));

        var provider = CreateProvider(handler, FastOptions with { MaxAttempts = 3 });

        await Assert.ThrowsAsync<ProviderException>(() => provider.CompleteAsync(Request()));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Slow_upstream_is_reported_as_timeout()
    {
        var handler = new SlowHttpHandler(TimeSpan.FromSeconds(5));
        var provider = CreateProvider(handler, FastOptions with { MaxAttempts = 1, Timeout = TimeSpan.FromMilliseconds(50) });

        var ex = await Assert.ThrowsAsync<ProviderException>(() => provider.CompleteAsync(Request()));

        Assert.Equal(ProviderErrorKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task Response_without_choices_is_reported_as_invalid_response()
    {
        var handler = new StubHttpHandler((_, _) => Json(HttpStatusCode.OK, """
          {
            "id": "chatcmpl-1",
            "model": "gpt-4o-mini",
            "choices": [],
            "usage": { "prompt_tokens": 1, "completion_tokens": 1 }
          }
          """));

        var provider = CreateProvider(handler);

        var ex = await Assert.ThrowsAsync<ProviderException>(() => provider.CompleteAsync(Request()));

        Assert.Equal(ProviderErrorKind.InvalidResponse, ex.Kind);
    }
}
