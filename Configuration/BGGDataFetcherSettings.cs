namespace BGGDataFetcher.Configuration;

public class BGGDataFetcherSettings
{
  /// <summary>
  /// Folder for the working files (LoadFileName, SaveBasicFileName, SaveDetailedFileName and the .jsonl data file).
  /// Created automatically. A file name that is an absolute path is used as is.
  /// </summary>
  public string OutputFolder { get; set; } = "output";

  public bool LoadFromFile { get; set; } = false;
  /// <summary>
  /// Data dump zip. With a wildcard (default) the file with the newest yyyy-MM-dd date in its name is used.
  /// </summary>
  public string DataDumpFileName { get; set; } = "Data/boardgames_ranks_*.zip";
  public int Count { get; set; } = 100;

  /// <summary>
  /// true: only ranked games, stopping when the ranked games run out (even if fewer than Count).
  /// false: continue with unranked games until Count is reached.
  /// </summary>
  public bool RankedOnly { get; set; } = true;
  public string LoadFileName { get; set; } = "TopGames.json";
  public string SaveBasicFileName { get; set; } = "TopGames.json";

  /// <summary>
  /// Final detailed output (JSON array). Progress is stored next to it in an
  /// append-only .jsonl file with the same name, which is also what makes runs resumable.
  /// </summary>
  public string SaveDetailedFileName { get; set; } = "TopGamesDetailed.json";
  public bool FetchGameDetails { get; set; } = true;

  /// <summary>
  /// 0 (default): games already fetched are never fetched again.
  /// N &gt; 0: games whose details are older than N days are fetched again and replaced.
  /// </summary>
  public int RefreshAfterDays { get; set; } = 0;
  public bool SaveIndividualJsonFiles { get; set; } = false;
  public string IndividualJsonOutputFolder { get; set; } = "output/games";

  // Full paths of the working files (file names resolved against OutputFolder)
  public string LoadFilePath => ResolveInOutputFolder(LoadFileName);
  public string BasicFilePath => ResolveInOutputFolder(SaveBasicFileName);
  public string DetailedFilePath => ResolveInOutputFolder(SaveDetailedFileName);

  /// <summary>Append-only data file next to the detailed output (same name, .jsonl); makes runs resumable.</summary>
  public string DetailedDataFilePath => Path.ChangeExtension(DetailedFilePath, ".jsonl");

  private string ResolveInOutputFolder(string fileName) =>
    Path.IsPathRooted(fileName) ? fileName : Path.Combine(OutputFolder, fileName);
}
