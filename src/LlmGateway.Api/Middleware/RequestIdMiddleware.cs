namespace LlmGateway.Api.Middleware;

public sealed class RequestIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Request-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var requestId = string.IsNullOrWhiteSpace(incoming) ? context.TraceIdentifier : incoming.Trim();

        context.TraceIdentifier = requestId;
        context.Items[HttpContextItems.RequestId] = requestId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = requestId;
            return Task.CompletedTask;
        });

        await next(context);
    }
}
