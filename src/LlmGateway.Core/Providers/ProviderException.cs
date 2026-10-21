namespace LlmGateway.Core.Providers;

public enum ProviderErrorKind
{
    Upstream,
    Timeout,
    Unreachable,
    InvalidResponse,
}

public sealed class ProviderException : Exception
{
    public ProviderException(
        string provider,
        ProviderErrorKind kind,
        string message,
        int? upstreamStatusCode = null,
        bool retryable = false,
        TimeSpan? retryAfter = null)
        : base(message)
    {
        Provider = provider;
        Kind = kind;
        UpstreamStatusCode = upstreamStatusCode;
        Retryable = retryable;
        RetryAfter = retryAfter;
    }

    public string Provider { get; }

    public ProviderErrorKind Kind { get; }

    public int? UpstreamStatusCode { get; }

    public bool Retryable { get; }

    public TimeSpan? RetryAfter { get; }
}
