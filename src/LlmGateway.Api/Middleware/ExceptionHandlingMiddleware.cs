using System.Text.Json;
using LlmGateway.Core.Providers;
using Microsoft.AspNetCore.Mvc;

namespace LlmGateway.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ProviderException ex)
        {
            logger.LogWarning(
                "Upstream provider {Provider} failed: {Kind} (upstream status {UpstreamStatus}).",
                ex.Provider,
                ex.Kind,
                ex.UpstreamStatusCode);

            var status = MapStatus(ex);

            if (status == StatusCodes.Status429TooManyRequests && ex.RetryAfter is { } retryAfter)
            {
                context.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            }

            await WriteProblemAsync(context, status, MapTitle(ex), ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled error while processing {Method} {Path}.",
                context.Request.Method,
                context.Request.Path);

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                "The gateway could not complete the request.");
        }
    }

    private static int MapStatus(ProviderException ex) => ex.Kind switch
    {
        ProviderErrorKind.Timeout => StatusCodes.Status504GatewayTimeout,
        ProviderErrorKind.Unreachable => StatusCodes.Status503ServiceUnavailable,
        ProviderErrorKind.InvalidResponse => StatusCodes.Status502BadGateway,
        _ => ex.UpstreamStatusCode switch
        {
            429 => StatusCodes.Status429TooManyRequests,
            >= 500 => StatusCodes.Status503ServiceUnavailable,
            >= 400 => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status502BadGateway,
        },
    };

    private static string MapTitle(ProviderException ex) => ex.Kind switch
    {
        ProviderErrorKind.Timeout => "Upstream provider timed out",
        ProviderErrorKind.Unreachable => "Upstream provider unavailable",
        ProviderErrorKind.InvalidResponse => "Upstream provider returned an invalid response",
        _ => ex.UpstreamStatusCode == 429 ? "Upstream provider rate limited" : "Upstream provider error",
    };

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path,
        };

        problem.Extensions["requestId"] =
            context.Items[HttpContextItems.RequestId]?.ToString() ?? context.TraceIdentifier;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, "application/problem+json");
    }
}
