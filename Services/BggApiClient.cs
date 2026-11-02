using System.Net;
using BGGDataFetcher.Interfaces;
using BGGDataFetcher.Models;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

public class BggApiClient(
  HttpClient httpClient,
  XmlProcessor xmlProcessor,
  ErrorLog errorLog,
  ILogger<BggApiClient> logger,
  IConsoleOutput output)
{
    private const string BGG_API_BASE = "https://boardgamegeek.com/xmlapi2";
    private const int MAX_ATTEMPTS = 6;
    private const double MIN_RETRY_DELAY_SECONDS = 1;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(2);

    // BGG limits the number of calls in a period of time. When a request is rate limited (429) we
    //  1. wait RateLimitWait (a longer Retry-After from the server wins) and try the same request again, and
    //  2. make the pause between all following requests a little longer, for the rest of the run:
    //     1s -> 1.5s -> 2s -> 2.5s ... up to 10s. Every 429 adds one step.
    private const double REQUEST_DELAY_START_SECONDS = 1;
    private const double REQUEST_DELAY_STEP_SECONDS = 0.5;
    private const double REQUEST_DELAY_MAX_SECONDS = 10;

    /// <summary>How long to wait before retrying a request that was rate limited (429).</summary>
    public TimeSpan RateLimitWait { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The pause callers should make between two requests. Starts at 1 second and grows by half a second
    /// each time BGG answers 429, up to 10 seconds. It is not reset during the run.
    /// </summary>
    public TimeSpan RequestDelay { get; private set; } = TimeSpan.FromSeconds(REQUEST_DELAY_START_SECONDS);

    private TimeSpan OnRateLimited(TimeSpan? retryAfter)
    {
        var next = Math.Min(REQUEST_DELAY_MAX_SECONDS, RequestDelay.TotalSeconds + REQUEST_DELAY_STEP_SECONDS);
        if (next > RequestDelay.TotalSeconds)
        {
            RequestDelay = TimeSpan.FromSeconds(next);
            logger.WarnAndShow(output, "  ⚠ Rate limited (429) - pause between requests is now {Delay:F1}s", RequestDelay.TotalSeconds);
        }

        return retryAfter is { } serverWait && serverWait > RateLimitWait ? serverWait : RateLimitWait;
    }

    /// <summary>
    /// Fetches details for the given game IDs. Transient failures (429, 5xx, timeouts, queued/empty responses)
    /// are retried with backoff; IDs that still could not be fetched are simply missing from the result.
    /// </summary>
    public async Task<List<BoardGameDetailed>> FetchGameDetailsBatchAsync(IReadOnlyList<string> gameIds, CancellationToken ct = default)
    {
        var url = $"{BGG_API_BASE}/thing?id={string.Join(",", gameIds)}&stats=1";

        var xmlContent = await GetXmlWithRetryAsync(url, ct);
        if (xmlContent == null)
        {
            errorLog.Write($"Giving up on game IDs [{string.Join(", ", gameIds)}] after {MAX_ATTEMPTS} attempts");
            return [];
        }

        var games = xmlProcessor.TryProcessXmlGames(xmlContent, errorLog.Write);
        if (games != null)
        {
            return games;
        }

        // The response is XML-like but broken - narrow down which game(s) cause it
        if (gameIds.Count == 1)
        {
            errorLog.Write($"XML parsing failed for game ID {gameIds[0]}, skipping it");
            return [];
        }

        errorLog.Write($"XML parsing error for game IDs [{string.Join(", ", gameIds)}], splitting batch");
        logger.WarnAndShow(output, "XML parsing failed for a batch of {Count} games, splitting into smaller batches...", gameIds.Count);

        var midpoint = gameIds.Count / 2;
        List<BoardGameDetailed> combined = [];
        combined.AddRange(await FetchGameDetailsBatchAsync(gameIds.Take(midpoint).ToList(), ct));
        await Task.Delay(RequestDelay, ct);
        combined.AddRange(await FetchGameDetailsBatchAsync(gameIds.Skip(midpoint).ToList(), ct));
        return combined;
    }

    /// <summary>Returns the response body, or null if all attempts failed.</summary>
    private async Task<string?> GetXmlWithRetryAsync(string url, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
        {
            TimeSpan delay;
            string reason;

            try
            {
                using var response = await httpClient.GetAsync(url, ct);

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);

                    // 202 means "queued, try again later"; an empty or non-XML body is a notice page, not data
                    if (response.StatusCode != HttpStatusCode.Accepted && body.Contains("<items", StringComparison.Ordinal))
                    {
                        return body;
                    }

                    reason = response.StatusCode == HttpStatusCode.Accepted ? "202 Accepted (queued)" : "empty or non-XML response";
                    delay = GetBackoff(attempt);
                }
                else if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new UnauthorizedAccessException(
                      $"BGG API rejected the bearer token ({(int)response.StatusCode} {response.StatusCode}). Check BggApi:BearerToken.");
                }
                else if (!IsTransient(response.StatusCode))
                {
                    errorLog.Write($"Request failed with {(int)response.StatusCode} {response.StatusCode}: {url}");
                    return null;
                }
                else
                {
                    reason = $"{(int)response.StatusCode} {response.StatusCode}";
                    delay = response.StatusCode == HttpStatusCode.TooManyRequests
                        ? OnRateLimited(GetRetryAfter(response))
                        : GetRetryAfter(response) ?? GetBackoff(attempt);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                delay = GetBackoff(attempt);
            }

            if (attempt == MAX_ATTEMPTS)
            {
                errorLog.Write($"Attempt {attempt}/{MAX_ATTEMPTS} failed ({reason}): {url}");
                break;
            }

            // Retry-After can be zero or a date in the past: never retry without a pause, never wait longer than the cap
            delay = TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, MIN_RETRY_DELAY_SECONDS, MaxRetryDelay.TotalSeconds));

            logger.WarnAndShow(output, "  ⚠ Request failed ({Reason}) - retrying in {Delay:F0}s (attempt {Attempt}/{Max})",
              reason, delay.TotalSeconds, attempt, MAX_ATTEMPTS);
            await Task.Delay(delay, ct);
        }

        return null;
    }

    private static bool IsTransient(HttpStatusCode status) =>
      status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)status >= 500;

    private static TimeSpan GetBackoff(int attempt) =>
      TimeSpan.FromSeconds(Math.Min(60, 5 * Math.Pow(2, attempt - 1)) + Random.Shared.NextDouble());

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta) return delta;
        if (retryAfter?.Date is { } date) return date - DateTimeOffset.UtcNow;
        return null;
    }
}
