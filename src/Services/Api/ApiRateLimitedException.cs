namespace WikeloContractor.Services.Api;

/// <summary>Thrown when the API responds with HTTP 429 (too many requests per minute).</summary>
public sealed class ApiRateLimitedException(TimeSpan? retryAfter)
    : Exception("The API rate limit has been exceeded.")
{
    /// <summary>Server-suggested wait time from the Retry-After header, when present.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
