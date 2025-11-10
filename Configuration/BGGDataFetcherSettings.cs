namespace BGGDataFetcher.Configuration;

public class BGGDataFetcherSettings
{
  public bool LoadFromFile { get; set; } = false;
  public string DataDumpFileName { get; set; } = "boardgames_ranks_2025-11-07.zip";
  public int Count { get; set; } = 100;
  public int StartPosition { get; set; } = 0; // Start position for fetching details (0-based index)
  public string LoadFileName { get; set; } = "TopGames.json";
  public string SaveBasicFileName { get; set; } = "TopGames.json";
  public string SaveDetailedFileName { get; set; } = "TopGamesDetailed.json";
  public bool FetchGameDetails { get; set; } = true;
}
