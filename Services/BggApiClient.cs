using BGGDataFetcher.Models;
using BGGDataFetcher.Configuration;
using BGGDataFetcher.Interfaces;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

public class BggApiClient(
  HttpClient httpClient,
  BggApiSettings settings,
  ILogger<BggApiClient> logger,
  ILogger<FileManager> fileManagerLogger,
  IConsoleOutput? output = null)
{
  private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
  private readonly XmlProcessor _xmlProcessor = new(logger);
  private readonly FileManager _fileManager = new(fileManagerLogger);
  private readonly ILogger<BggApiClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
  private readonly IConsoleOutput? _output = output;
  private readonly string _bearerToken = settings?.BearerToken ?? throw new ArgumentNullException(nameof(settings));

  private const string BGG_API_BASE = "https://boardgamegeek.com/xmlapi2";
  private const int SAVE_INTERVAL = 300; // Save progress every 300 games
  private const string ERROR_LOG_FILE = "bgg_errors.log";
  private const int RATE_LIMIT_PAUSE_MS = 5000;
  private const int DELAY_INCREMENT_MS = 500;

  private void LogInfo(string message, params object[] args)
  {
    _logger.LogInformation(message, args);
    _output?.WriteInfo(message, args);
  }

  private void LogWarning(string message, params object[] args)
  {
    _logger.LogWarning(message, args);
    _output?.WriteWarning(message, args);
  }

  private void LogDebug(string message, params object[] args)
  {
    _logger.LogDebug(message, args);
    _output?.WriteDebug(message, args);
  }

  private async Task LogErrorAsync(string message)
  {
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    var logMessage = $"[{timestamp}] {message}{Environment.NewLine}";
    await File.AppendAllTextAsync(ERROR_LOG_FILE, logMessage);
    _logger.LogError("{Message}", message);
  }

  private async Task<string> FetchXmlFromApiAsync(string url)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Add("Authorization", $"Bearer {_bearerToken}");

    var response = await _httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();

    return await response.Content.ReadAsStringAsync();
  }

  private string BuildApiUrl(List<string> gameIds)
  {
    var ids = string.Join(",", gameIds);
    return $"{BGG_API_BASE}/thing?id={ids}&stats=1";
  }

  private async Task<List<BoardGameDetailed>> SplitAndRetryBatchAsync(List<string> gameIds, string ids, Exception ex)
  {
    await LogErrorAsync($"XML parsing error for game IDs [{string.Join(", ", gameIds)}]: {ex.Message}");
    LogWarning("XML parsing failed for batch of {Count} games [{GameIds}], splitting into smaller batches...",
      gameIds.Count, ids);

    var midpoint = gameIds.Count / 2;
    var firstHalf = gameIds.Take(midpoint).ToList();
    var secondHalf = gameIds.Skip(midpoint).ToList();

    LogInfo("  Splitting batch: First half ({Count} games), Second half ({Count2} games)",
    firstHalf.Count, secondHalf.Count);

    // Recursively fetch both halves
    var firstResults = await FetchGameDetailsBatchAsync(firstHalf);
    var secondResults = await FetchGameDetailsBatchAsync(secondHalf);

    // Combine results
    List<BoardGameDetailed> combinedResults = [];
    combinedResults.AddRange(firstResults);
    combinedResults.AddRange(secondResults);
    return combinedResults;
  }

  #region Game Enrichment Methods

  private void MergeBasicInfoIntoDetailedGame(BoardGameDetailed detailedGame, BoardGameBasic basicGame)
  {
    // The API data takes precedence, but we can ensure basic data is present
    // The enricher already sets these, but this is a safety net
    if (string.IsNullOrEmpty(detailedGame.Name))
      detailedGame.Name = basicGame.Name;
    if (detailedGame.YearPublished == 0)
      detailedGame.YearPublished = basicGame.YearPublished;
    if (!detailedGame.Rank.HasValue)
      detailedGame.Rank = basicGame.Rank;
    if (!detailedGame.BayesAverage.HasValue)
      detailedGame.BayesAverage = basicGame.BayesAverage;
    // Ensure NumId is set from basic game if not already set
    if (detailedGame.NumId == 0)
      detailedGame.NumId = basicGame.NumId;
  }

  private void EnrichBatchWithBasicInfo(List<BoardGameDetailed> batchDetailedGames, List<BoardGameBasic> batch)
  {
    foreach (var detailedGame in batchDetailedGames)
    {
      var basicGame = batch.FirstOrDefault(g => g.Id == detailedGame.Id);
      if (basicGame != null)
      {
        MergeBasicInfoIntoDetailedGame(detailedGame, basicGame);
      }
    }
  }

  #endregion

  #region Progress Saving Methods

  private bool ShouldSaveProgress(int detailedGamesCount, int processedCount, int batchSize)
  {
    if (detailedGamesCount % SAVE_INTERVAL != 0 &&
        detailedGamesCount < SAVE_INTERVAL * (detailedGamesCount / SAVE_INTERVAL + 1))
      return false;

    var checkpointCount = (processedCount / SAVE_INTERVAL) * SAVE_INTERVAL;
    return checkpointCount > 0 &&
              processedCount >= checkpointCount &&
              processedCount % SAVE_INTERVAL < batchSize;
  }

  private async Task SaveProgressAsync(List<BoardGameDetailed> detailedGames, string saveFileName, int processedCount)
  {
    var progressFileName = saveFileName.Replace(".json", $"_progress_{processedCount}.json");
    await _fileManager.SaveDetailedGamesToJsonAsync(detailedGames, progressFileName);
    LogInfo("  💾 Progress saved to {FileName} ({Count} games processed)", progressFileName, processedCount);
  }

  #endregion

  #region Batch Processing Methods

  private async Task<List<BoardGameDetailed>> FetchSingleGameAsync(string gameId)
  {
    var url = BuildApiUrl([gameId]);

    try
    {
      var xmlContent = await FetchXmlFromApiAsync(url);
      var games = await _xmlProcessor.TryProcessXmlGamesAsync(xmlContent, async msg => await LogErrorAsync(msg), LogWarning);
      return games ?? [];
    }
    catch (Exception ex)
    {
      await LogErrorAsync($"Failed to fetch game ID {gameId}: {ex.GetType().Name} - {ex.Message}");
      LogWarning("Skipping game ID {GameId} due to persistent errors", gameId);
      return [];
    }
  }

  public async Task<List<BoardGameDetailed>> FetchGameDetailsBatchAsync(List<string> gameIds)
  {
    // Base case: if only one game ID and it fails, return empty list
    if (gameIds.Count == 1)
    {
      return await FetchSingleGameAsync(gameIds[0]);
    }

    // Try to fetch all game IDs in one batch
    var ids = string.Join(",", gameIds);
    var batchUrl = BuildApiUrl(gameIds);

    try
    {
      var xmlContent = await FetchXmlFromApiAsync(batchUrl);

      // Try to parse and process XML
      var games = await _xmlProcessor.TryProcessXmlGamesAsync(xmlContent, async msg => await LogErrorAsync(msg), LogWarning);

      if (games == null)
      {
        // XML parsing failed - split batch in half and retry
        return await SplitAndRetryBatchAsync(gameIds, ids, new Exception("XML parsing failed"));
      }

      return games;
    }
    catch (HttpRequestException)
    {
      // HTTP request failed - rethrow to let caller handle
      throw;
    }
  }

  private async Task<(int newDelay, bool shouldRetry)> HandleRateLimitAsync(
    int batchIndex,
    int totalBatches,
    int startPosition,
  int batchSize,
    int basicGamesCount,
int currentDelay)
  {
    var errorMsg = $"Rate limited (429) at batch {batchIndex + 1}/{totalBatches}, " +
        $"games {startPosition + (batchIndex * batchSize) + 1} to " +
        $"{Math.Min(startPosition + ((batchIndex + 1) * batchSize), basicGamesCount)}";

    await LogErrorAsync(errorMsg);
    LogWarning("  ⚠ Rate limited (429) - Pausing for {Delay} seconds...", RATE_LIMIT_PAUSE_MS / 1000);

    await Task.Delay(RATE_LIMIT_PAUSE_MS);

    // Increase delay
    var newDelay = currentDelay + DELAY_INCREMENT_MS;
    LogInfo("  ℹ Increased delay to {Delay}ms for subsequent requests", newDelay);

    return (newDelay, true); // Return new delay and indicate retry
  }

  private async Task HandleBatchErrorAsync(Exception ex, int batchIndex, int totalBatches, List<BoardGameBasic> batch)
  {
    var gameIds = batch.Select(g => g.Id).ToList();
    var errorMsg = $"Failed to fetch batch {batchIndex + 1}/{totalBatches}, " +
            $"game IDs [{string.Join(", ", gameIds)}]: {ex.GetType().Name} - {ex.Message}";

    await LogErrorAsync(errorMsg);

    if (ex.InnerException != null)
    {
      await LogErrorAsync($"Inner exception: {ex.InnerException.Message}");
    }

    LogWarning("  ⚠ Warning: Could not fetch detailed data for batch - {ErrorMessage}", ex.Message);
    LogInfo("  Error logged to {LogFile}", ERROR_LOG_FILE);
  }

  #endregion

  public async Task<List<BoardGameDetailed>> EnrichGamesWithDetailsAsync(
    List<BoardGameBasic> basicGames,
    int batchSize,
    int delayMs,
    string saveFileName,
    int startPosition = 0)
  {
    LogInfo("Fetching detailed game information from BGG API...");

    List<BoardGameDetailed> detailedGames = [];
    var gamesToProcess = basicGames.Skip(startPosition).ToList();
    var totalBatches = (int)Math.Ceiling(gamesToProcess.Count / (double)batchSize);
    var currentDelay = delayMs;
    var processedCount = startPosition;

    for (int i = 0; i < totalBatches; i++)
    {
      var batch = gamesToProcess.Skip(i * batchSize).Take(batchSize).ToList();

      try
      {
        var batchStart = startPosition + (i * batchSize) + 1;
        var batchEnd = Math.Min(startPosition + ((i + 1) * batchSize), basicGames.Count);
        LogInfo("[Batch {BatchNum}/{TotalBatches}] Fetching detailed info for games {Start} to {End}...", i + 1, totalBatches, batchStart, batchEnd);

        var ids = batch.Select(g => g.Id).ToList();
        var batchDetailedGames = await FetchGameDetailsBatchAsync(ids);

        // Enrich with basic info
        EnrichBatchWithBasicInfo(batchDetailedGames, batch);

        detailedGames.AddRange(batchDetailedGames);
        processedCount += batchDetailedGames.Count;
        LogInfo("  ✓ Fetched detailed info for {Count} games (Total processed: {Total})", batchDetailedGames.Count, processedCount);

        // Save progress if needed
        if (ShouldSaveProgress(detailedGames.Count, processedCount, batchSize))
        {
          await SaveProgressAsync(detailedGames, saveFileName, processedCount);
        }

        // Delay before next request
        if (i < totalBatches - 1)
        {
          await Task.Delay(currentDelay);
        }
      }
      catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
      {
        var (newDelay, shouldRetry) = await HandleRateLimitAsync(i, totalBatches, startPosition, batchSize, basicGames.Count, currentDelay);
        currentDelay = newDelay;
        if (shouldRetry)
        {
          i--; // Retry the same batch
        }
      }
      catch (Exception ex)
      {
        await HandleBatchErrorAsync(ex, i, totalBatches, batch);
      }
    }

    LogInfo("Detailed enrichment completed!");
    LogInfo("Total games processed: {Count}", processedCount);
    if (startPosition > 0)
    {
      LogInfo("Note: Started from position {Position}. To get complete data, merge with previous progress files.",
              startPosition);
    }

    return detailedGames;
  }
}
