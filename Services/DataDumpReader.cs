using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using BGGDataFetcher.Models;
using CsvHelper;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

public partial class DataDumpReader(ILogger<DataDumpReader> logger)
{
  private const string CSV_FILE_NAME = "boardgames_ranks.csv";

  /// <summary>
  /// Reads the top <paramref name="count"/> base games from the BGG data dump, best rank first.
  /// Expansions are always skipped. With <paramref name="rankedOnly"/> the list stops where the ranked games end;
  /// otherwise unranked games (in dump order) fill up the list until <paramref name="count"/> is reached.
  /// <c>RankPos</c> is the position among the returned games.
  /// </summary>
  public List<BoardGameBasic> ReadFromDataDump(string zipFilePath, int count, bool rankedOnly = true)
  {
    zipFilePath = ResolveDataDumpPath(zipFilePath);

    logger.LogInformation("Reading from data dump: {FilePath}...", zipFilePath);

    List<BoardGameBasic> candidates = [];
    int skipped = 0;

    using (var archive = ZipFile.OpenRead(zipFilePath))
    {
      var csvEntry = archive.GetEntry(CSV_FILE_NAME)
        ?? throw new InvalidOperationException($"CSV file '{CSV_FILE_NAME}' not found in archive.");

      using var reader = new StreamReader(csvEntry.Open());
      using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

      csv.Read();
      csv.ReadHeader();

      while (csv.Read())
      {
        try
        {
          var game = ParseRow(csv);
          if (!game.IsExpansion && (game.Rank.HasValue || !rankedOnly))
          {
            candidates.Add(game);
          }
          else
          {
            skipped++;
          }
        }
        catch (Exception ex)
        {
          logger.LogWarning("Skipping unreadable data dump row {Row}: {ErrorMessage}", csv.Parser.Row, ex.Message);
        }
      }
    }

    // Ranked games first by rank (do not rely on the dump being sorted); unranked ones keep dump order (stable sort)
    var games = candidates.OrderBy(g => g.Rank ?? int.MaxValue).Take(count).ToList();
    for (int i = 0; i < games.Count; i++)
    {
      games[i].RankPos = i + 1;
    }

    logger.LogInformation("✓ Read {Count} games from data dump ({Available} eligible, {Skipped} rows skipped; rankedOnly={RankedOnly})",
      games.Count, candidates.Count, skipped, rankedOnly);

    if (games.Count < count)
    {
      logger.LogWarning("Requested {Requested} games but the data dump only contains {Available} eligible games (rankedOnly={RankedOnly})",
        count, candidates.Count, rankedOnly);
    }

    return games;
  }

  [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
  private static partial Regex DateInFileName();

  /// <summary>
  /// Returns the data dump to read. A plain path is used as is. A path containing a wildcard
  /// (e.g. <c>Data/boardgames_ranks_*.zip</c>) picks the match with the newest date in its file name
  /// (yyyy-MM-dd), so newly downloaded dumps are used without changing any configuration.
  /// </summary>
  public static string ResolveDataDumpPath(string pathOrPattern)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(pathOrPattern);

    bool isPattern = pathOrPattern.IndexOfAny(['*', '?']) >= 0;
    var fileName = Path.GetFileName(pathOrPattern);
    var directory = Path.GetDirectoryName(pathOrPattern);
    if (string.IsNullOrEmpty(directory)) directory = ".";

    // A relative path is looked up from the working folder first, then from the folder of the exe and its parents,
    // so "Data/..." is found both when running from the project folder and from bin/Debug/... during development
    foreach (var folder in CandidateFolders(directory))
    {
      if (!Directory.Exists(folder)) continue;

      if (!isPattern)
      {
        var exact = Path.Combine(folder, fileName);
        if (File.Exists(exact)) return exact;
        continue;
      }

      var newest = Directory.GetFiles(folder, fileName)
        .Select(file => (File: file, Date: DateInFileName().Match(Path.GetFileName(file))))
        .Where(x => x.Date.Success)
        .OrderByDescending(x => x.Date.Value, StringComparer.Ordinal) // yyyy-MM-dd sorts correctly as text
        .ThenByDescending(x => x.File, StringComparer.OrdinalIgnoreCase)
        .Select(x => x.File)
        .FirstOrDefault();

      if (newest != null) return newest;
    }

    throw new FileNotFoundException(isPattern
      ? $"No data dump matching '{pathOrPattern}' with a date (yyyy-MM-dd) in its name was found."
      : $"Data dump file not found: {pathOrPattern}");
  }

  private static IEnumerable<string> CandidateFolders(string directory)
  {
    if (Path.IsPathRooted(directory))
    {
      yield return directory;
      yield break;
    }

    yield return Path.GetFullPath(directory);

    var parent = new DirectoryInfo(AppContext.BaseDirectory);
    for (int level = 0; level < 6 && parent != null; level++, parent = parent.Parent)
    {
      yield return Path.GetFullPath(Path.Combine(parent.FullName, directory));
    }
  }

  private static BoardGameBasic ParseRow(CsvReader csv)
  {
    var id = csv.GetField<int>("id");

    return new BoardGameBasic
    {
      Id = id.ToString(CultureInfo.InvariantCulture),
      NumId = id,
      Name = csv.GetField<string>("name") ?? string.Empty,
      YearPublished = csv.GetField<int?>("yearpublished") ?? 0,
      Rank = csv.GetField<int?>("rank") is > 0 and var rank ? rank : null, // 0 means unranked
      BayesAverage = csv.GetField<double?>("bayesaverage"),
      Average = csv.GetField<double?>("average"),
      UsersRated = csv.GetField<int?>("usersrated"),
      IsExpansion = csv.GetField<int?>("is_expansion") == 1,
      AbstractsRank = csv.GetField<int?>("abstracts_rank"),
      CgsRank = csv.GetField<int?>("cgs_rank"),
      ChildrensGamesRank = csv.GetField<int?>("childrensgames_rank"),
      FamilyGamesRank = csv.GetField<int?>("familygames_rank"),
      PartyGamesRank = csv.GetField<int?>("partygames_rank"),
      StrategyGamesRank = csv.GetField<int?>("strategygames_rank"),
      ThematicRank = csv.GetField<int?>("thematic_rank"),
      WarGamesRank = csv.GetField<int?>("wargames_rank")
    };
  }
}
