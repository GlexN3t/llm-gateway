namespace LlmGateway.Core.Providers;

public sealed record ProviderTransportOptions(
    int MaxAttempts = 3,
    TimeSpan? Timeout = null,
    TimeSpan? BaseDelay = null)
{
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromSeconds(60);

    public TimeSpan EffectiveBaseDelay => BaseDelay ?? TimeSpan.FromMilliseconds(250);
}
