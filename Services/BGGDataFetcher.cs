using BGGDataFetcher.Models;
using BGGDataFetcher.Services;
using BGGDataFetcher.Configuration;
using BGGDataFetcher.Interfaces;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher;

public class BGGDataFetcher
{
  private readonly HttpClient _httpClient;
  private readonly BggApiClient _apiClient;
  private readonly FileManager _fileManager;
  private readonly DataDumpReader _dataDumpReader;
  private readonly ILogger<BGGDataFetcher> _logger;
  private readonly IConsoleOutput? _output;

  private const int API_BATCH_SIZE = 20; // Number of games to fetch details for in one API call
  private const int DELAY_MS = 1000; // Delay between API requests

  public BGGDataFetcher(BggApiSettings settings, ILogger<BGGDataFetcher> logger, ILogger<BggApiClient> apiLogger, ILogger<DataDumpReader> dataDumpLogger, ILogger<FileManager> fileManagerLogger, IConsoleOutput? output = null)
  {
    _httpClient = new HttpClient();
    _httpClient.DefaultRequestHeaders.Add("Accept", "application/xml");
    _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");

    _logger = logger;
 _output = output;
    _apiClient = new BggApiClient(_httpClient, settings, apiLogger, fileManagerLogger, output);
    _fileManager = new FileManager(fileManagerLogger);
    _dataDumpReader = new DataDumpReader(dataDumpLogger);
  }

  public List<BoardGameBasic> FetchTopGamesFromDataDump(int count, string dataDumpFileName, string saveFileName = "TopGames.json")
  {
 _logger.LogInformation("Fetching top {Count} ranked games from data dump...", count);

    // Read from data dump
    var basicGames = _dataDumpReader.ReadFromDataDump(dataDumpFileName, count);

    // Save basic game list
    _fileManager.SaveBasicGamesToJson(basicGames, saveFileName);

    _logger.LogInformation("? Successfully fetched {Count} games!", basicGames.Count);
    _logger.LogInformation("Basic game data saved to {FileName}", saveFileName);

    return basicGames;
  }

  public async Task<List<BoardGameDetailed>> FetchGameDetailsAsync(List<BoardGameBasic> basicGames, string saveFileName = "TopGamesDetailed.json")
  {
    _logger.LogInformation("Fetching detailed information for {Count} games from BGG API...", basicGames.Count);

 // Enrich the data with detailed API information
    var detailedGames = await _apiClient.EnrichGamesWithDetailsAsync(basicGames, API_BATCH_SIZE, DELAY_MS, saveFileName);

  // Save final detailed game data
    _fileManager.SaveDetailedGamesToJson(detailedGames, saveFileName);

    return detailedGames;
  }

  public async Task<List<BoardGameDetailed>> FetchGameDetailsAsync(List<BoardGameBasic> basicGames, string saveFileName, int startPosition)
  {
  _logger.LogInformation("Fetching detailed information for {Count} games from BGG API...", basicGames.Count);
    _logger.LogInformation("Starting from position {Position}", startPosition);

    // Enrich the data with detailed API information
    var detailedGames = await _apiClient.EnrichGamesWithDetailsAsync(basicGames, API_BATCH_SIZE, DELAY_MS, saveFileName, startPosition);

    // Save final detailed game data
    _fileManager.SaveDetailedGamesToJson(detailedGames, saveFileName);

    return detailedGames;
  }

  public void DisplaySummary(List<BoardGameDetailed> games)
  {
    _output?.WriteLine();
    _output?.WriteInfo("Summary:");
    _output?.WriteInfo(new string('=', 60));
    _output?.WriteInfo("Total games fetched: {0}", games.Count);

    if (games.Any())
    {
      var gamesWithRank = games.Where(g => g.Rank.HasValue).ToList();
      if (gamesWithRank.Any())
      {
        _output?.WriteInfo("Games with rank data: {0}", gamesWithRank.Count);
      var minRank = gamesWithRank.Min(g => g.Rank!.Value);
        var maxRank = gamesWithRank.Max(g => g.Rank!.Value);
        _output?.WriteInfo("Highest rank: #{0}", minRank);
     _output?.WriteInfo("Lowest rank: #{0}", maxRank);
      }

      var gamesWithRating = games.Where(g => g.BayesAverage.HasValue).ToList();
      if (gamesWithRating.Any())
      {
        var avgRating = gamesWithRating.Average(g => g.BayesAverage!.Value);
        var maxRating = gamesWithRating.Max(g => g.BayesAverage!.Value);
    var minRating = gamesWithRating.Min(g => g.BayesAverage!.Value);
        _output?.WriteInfo("Average rating: {0:F2}", avgRating);
        _output?.WriteInfo("Highest rating: {0:F2}", maxRating);
    _output?.WriteInfo("Lowest rating: {0:F2}", minRating);
      }

   var gamesWithPlayers = games.Where(g => g.MinPlayers.HasValue && g.MaxPlayers.HasValue).ToList();
 if (gamesWithPlayers.Any())
      {
        var minPlayers = gamesWithPlayers.Min(g => g.MinPlayers!.Value);
      var maxPlayers = gamesWithPlayers.Max(g => g.MaxPlayers!.Value);
_output?.WriteInfo("Player count range: {0}-{1} players", minPlayers, maxPlayers);
      }

      var gamesWithCategories = games.Where(g => g.Categories.Any()).ToList();
      if (gamesWithCategories.Any())
 {
        _output?.WriteInfo("Games with categories: {0}", gamesWithCategories.Count);
}

      var gamesWithMechanics = games.Where(g => g.Mechanics.Any()).ToList();
  if (gamesWithMechanics.Any())
      {
        _output?.WriteInfo("Games with mechanics: {0}", gamesWithMechanics.Count);
      }
    }

    _output?.WriteInfo(new string('=', 60));
  }
}
