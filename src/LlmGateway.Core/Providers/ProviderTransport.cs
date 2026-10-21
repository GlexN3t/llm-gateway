namespace LlmGateway.Core.Providers;

internal static class ProviderTransport
{
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient http,
        Func<HttpRequestMessage> requestFactory,
        string provider,
        ProviderTransportOptions options,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(options.EffectiveTimeout);

            try
            {
                using var request = requestFactory();
                var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                var status = (int)response.StatusCode;
                var retryable = IsRetryable(status);
                var retryAfter = ReadRetryAfter(response);
                response.Dispose();

                if (!retryable || attempt >= options.MaxAttempts)
                {
                    throw new ProviderException(
                        provider,
                        ProviderErrorKind.Upstream,
                        $"{provider} returned HTTP {status}.",
                        status,
                        retryable,
                        retryAfter);
                }

                await BackoffAsync(attempt, retryAfter, options, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new ProviderException(
                    provider,
                    ProviderErrorKind.Timeout,
                    $"{provider} did not respond within {options.EffectiveTimeout.TotalSeconds:0}s.",
                    retryable: true);
            }
            catch (HttpRequestException ex)
            {
                if (attempt >= options.MaxAttempts)
                {
                    throw new ProviderException(
                        provider,
                        ProviderErrorKind.Unreachable,
                        $"{provider} is unreachable: {ex.Message}",
                        retryable: true);
                }

                await BackoffAsync(attempt, null, options, ct);
            }
        }
    }

    private static bool IsRetryable(int status) => status is 408 or 429 or 500 or 502 or 503 or 504;

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return null;
    }

    private static async Task BackoffAsync(
        int attempt,
        TimeSpan? retryAfter,
        ProviderTransportOptions options,
        CancellationToken ct)
    {
        var delay = options.EffectiveBaseDelay * Math.Pow(2, attempt - 1)
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));

        if (retryAfter is { } upstream && upstream > delay)
        {
            delay = upstream;
        }

        if (delay > TimeSpan.FromSeconds(10))
        {
            delay = TimeSpan.FromSeconds(10);
        }

        await Task.Delay(delay, ct);
    }
}
