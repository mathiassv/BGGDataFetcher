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
    private const int SPLIT_DELAY_MS = 1000; // pause between the requests of a split batch
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(2);

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
        await Task.Delay(SPLIT_DELAY_MS, ct);
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
                    delay = GetRetryAfter(response) ?? GetBackoff(attempt);
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
