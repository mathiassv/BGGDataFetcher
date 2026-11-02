using BGGDataFetcher.Configuration;
using BGGDataFetcher.Interfaces;
using BGGDataFetcher.Models;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

/// <summary>Result of a detail-fetching run.</summary>
/// <param name="JsonlPath">The data file (one JSON record per line).</param>
/// <param name="GameIds">Ids of the games this run was about; the output and summary only contain these.</param>
public record FetchResult(
  string JsonlPath,
  IReadOnlySet<string> GameIds,
  int AlreadyFetched,
  int Fetched,
  IReadOnlyList<string> FailedIds,
  bool Cancelled);

public class FetchService(
  BggApiClient apiClient,
  FileManager fileManager,
  DataDumpReader dataDumpReader,
  ErrorLog errorLog,
  BGGDataFetcherSettings settings,
  ILogger<FetchService> logger,
  IConsoleOutput output)
{
  private const int API_BATCH_SIZE = 20; // BGG allows at most 20 ids per "thing" request

  public async Task<List<BoardGameBasic>> FetchTopGamesFromDataDumpAsync(int count, string dataDumpFileName, string saveFilePath)
  {
    logger.LogInformation("Fetching top {Count} ranked games from data dump...", count);

    var dataDumpPath = DataDumpReader.ResolveDataDumpPath(dataDumpFileName);
    logger.InfoAndShow(output, "Using data dump: {DataDump}", dataDumpPath);

    var basicGames = dataDumpReader.ReadFromDataDump(dataDumpPath, count, settings.RankedOnly);
    await fileManager.SaveBasicGamesToJsonAsync(basicGames, saveFilePath);

    logger.LogInformation("✓ Successfully fetched {Count} games!", basicGames.Count);
    logger.LogInformation("Basic game data saved to {FileName}", saveFilePath);

    return basicGames;
  }

  /// <summary>
  /// Fetches details for all games that are not yet in the data file (<c>*.jsonl</c> next to the detailed output) -
  /// or are older than <c>RefreshAfterDays</c> - appending each batch as soon as it arrives.
  /// Re-running the app therefore resumes where it stopped and retries anything that failed.
  /// Afterwards the data file is brought up to date with the current data dump (ranks, order) and the final JSON
  /// (and optional per-game files) is written.
  /// </summary>
  public async Task<FetchResult> FetchGameDetailsAsync(List<BoardGameBasic> basicGames, CancellationToken ct = default)
  {
    var jsonlPath = settings.DetailedDataFilePath;

    // A hand-edited game list may contain the same game twice
    var distinctGames = basicGames.GroupBy(g => g.Id).Select(group => group.First()).ToList();
    if (distinctGames.Count != basicGames.Count)
    {
      logger.WarnAndShow(output, "Ignoring {Count} duplicate game id(s) in the game list", basicGames.Count - distinctGames.Count);
    }
    basicGames = distinctGames;

    var basicById = basicGames.ToDictionary(g => g.Id);
    var gameIds = basicById.Keys.ToHashSet();

    // A game needs fetching if it is missing, or (when RefreshAfterDays is set) its details are too old
    var fetchedAt = fileManager.GetFetchTimestamps(jsonlPath);
    DateTime? staleBefore = settings.RefreshAfterDays > 0 ? DateTime.UtcNow.AddDays(-settings.RefreshAfterDays) : null;

    bool IsStale(string id) => staleBefore.HasValue && (fetchedAt[id] is not { } at || at < staleBefore);

    var pending = basicGames.Where(g => !fetchedAt.ContainsKey(g.Id) || IsStale(g.Id)).ToList();
    int refreshing = pending.Count(g => fetchedAt.ContainsKey(g.Id));
    int upToDate = basicGames.Count - pending.Count;

    logger.InfoAndShow(output, "Fetching detailed game information from BGG API...");
    if (upToDate > 0 || refreshing > 0)
    {
      logger.InfoAndShow(output, "{UpToDate} games are up to date in {File}; {New} new and {Refresh} outdated games to fetch",
        upToDate, jsonlPath, pending.Count - refreshing, refreshing);
    }

    List<string> failedIds = [];
    int fetched = 0;
    int totalBatches = (int)Math.Ceiling(pending.Count / (double)API_BATCH_SIZE);
    bool cancelled = false;

    try
    {
      for (int i = 0; i < totalBatches; i++)
      {
        ct.ThrowIfCancellationRequested();

        var batch = pending.Skip(i * API_BATCH_SIZE).Take(API_BATCH_SIZE).ToList();
        var ids = batch.Select(g => g.Id).ToList();

        logger.InfoAndShow(output, "[Batch {BatchNum}/{TotalBatches}] Fetching games {Start} to {End}...",
          i + 1, totalBatches, batch[0].RankPos, batch[^1].RankPos);

        var games = (await apiClient.FetchGameDetailsBatchAsync(ids, ct))
          .Where(g => basicById.ContainsKey(g.Id))
          .ToList();

        var now = DateTime.UtcNow;
        foreach (var game in games)
        {
          game.ApplyDumpData(basicById[game.Id]);
          game.FetchedAt = now;
        }

        await fileManager.AppendDetailedGamesAsync(jsonlPath, games);
        fetched += games.Count;

        var returned = games.Select(g => g.Id).ToHashSet();
        var missing = ids.Where(id => !returned.Contains(id)).ToList();
        if (missing.Count > 0)
        {
          failedIds.AddRange(missing);
          errorLog.Write($"Batch {i + 1}/{totalBatches}: no data for game IDs [{string.Join(", ", missing)}]");
          logger.WarnAndShow(output, "  ⚠ No data for {Count} game(s) in this batch (see {LogFile})", missing.Count, ErrorLog.FileName);
        }

        logger.InfoAndShow(output, "  ✓ Saved {Count} games (Total fetched this run: {Total})", games.Count, fetched);

        if (i < totalBatches - 1)
        {
          // Starts at 1s and grows each time BGG rate limits us (see BggApiClient.RequestDelay)
          await Task.Delay(apiClient.RequestDelay, ct);
        }
      }
    }
    catch (OperationCanceledException)
    {
      cancelled = true;
      logger.WarnAndShow(output, "Interrupted. Progress is saved in {File} - run again to resume.", jsonlPath);
    }

    if (!cancelled)
    {
      // One pass: drop outdated records, apply the current dump's ranks to every game (also those not fetched
      // this run) and store the games in rank order
      fileManager.RewriteDetailedFile(jsonlPath, basicGames, (game, basic) => game.ApplyDumpData(basic));
      await WriteFinalOutputAsync(jsonlPath, gameIds);

      if (failedIds.Count > 0)
      {
        logger.WarnAndShow(output, "{Count} games could not be fetched. Run again to retry them.", failedIds.Count);
      }
    }

    return new FetchResult(jsonlPath, gameIds, upToDate, fetched, failedIds, cancelled);
  }

  private async Task WriteFinalOutputAsync(string jsonlPath, HashSet<string> wantedIds)
  {
    IEnumerable<BoardGameDetailed> Games() =>
      fileManager.ReadDetailedGames(jsonlPath).Where(g => wantedIds.Contains(g.Id));

    await fileManager.ExportJsonArrayAsync(Games(), settings.DetailedFilePath);

    if (settings.SaveIndividualJsonFiles)
    {
      await fileManager.SaveIndividualGameJsonFilesAsync(Games(), settings.IndividualJsonOutputFolder);
    }
  }

  public void DisplaySummary(FetchResult result)
  {
    int total = 0, withRank = 0, withRating = 0, withCategories = 0, withMechanics = 0;
    int? minRank = null, maxRank = null, minPlayers = null, maxPlayers = null;
    double ratingSum = 0, minRating = double.MaxValue, maxRating = double.MinValue;

    foreach (var g in fileManager.ReadDetailedGames(result.JsonlPath).Where(g => result.GameIds.Contains(g.Id)))
    {
      total++;
      if (g.Rank is { } rank)
      {
        withRank++;
        minRank = Math.Min(minRank ?? rank, rank);
        maxRank = Math.Max(maxRank ?? rank, rank);
      }
      if (g.BayesAverage is { } rating)
      {
        withRating++;
        ratingSum += rating;
        minRating = Math.Min(minRating, rating);
        maxRating = Math.Max(maxRating, rating);
      }
      if (g.MinPlayers is { } lo) minPlayers = Math.Min(minPlayers ?? lo, lo);
      if (g.MaxPlayers is { } hi) maxPlayers = Math.Max(maxPlayers ?? hi, hi);
      if (g.Categories.Count > 0) withCategories++;
      if (g.Mechanics.Count > 0) withMechanics++;
    }

    output.WriteLine();
    output.WriteInfo("Summary:");
    output.WriteInfo(new string('=', 60));
    output.WriteInfo("Total games in output: {0}", total);
    output.WriteInfo("Fetched this run: {0} (already present: {1}, failed: {2})", result.Fetched, result.AlreadyFetched, result.FailedIds.Count);

    if (withRank > 0)
    {
      output.WriteInfo("Games with rank data: {0}", withRank);
      output.WriteInfo("Highest rank: #{0}", minRank!);
      output.WriteInfo("Lowest rank: #{0}", maxRank!);
    }
    if (withRating > 0)
    {
      output.WriteInfo("Average rating: {0:F2}", ratingSum / withRating);
      output.WriteInfo("Highest rating: {0:F2}", maxRating);
      output.WriteInfo("Lowest rating: {0:F2}", minRating);
    }
    if (minPlayers.HasValue && maxPlayers.HasValue)
    {
      output.WriteInfo("Player count range: {0}-{1} players", minPlayers, maxPlayers);
    }
    output.WriteInfo("Games with categories: {0}", withCategories);
    output.WriteInfo("Games with mechanics: {0}", withMechanics);
    output.WriteInfo(new string('=', 60));
  }
}
