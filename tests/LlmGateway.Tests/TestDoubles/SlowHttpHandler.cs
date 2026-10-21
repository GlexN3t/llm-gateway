namespace LlmGateway.Tests.TestDoubles;

public sealed class SlowHttpHandler(TimeSpan delay) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
    }
}
