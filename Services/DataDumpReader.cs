using System.IO.Compression;
using BGGDataFetcher.Models;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

public class DataDumpReader(ILogger<DataDumpReader>? logger = null)
{
  private const string CSV_FILE_NAME = "boardgames_ranks.csv";
  private readonly ILogger<DataDumpReader>? _logger = logger;

  public List<BoardGameBasic> ReadFromDataDump(string zipFilePath, int count)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(zipFilePath);

    if (!File.Exists(zipFilePath))
    {
      throw new FileNotFoundException($"Data dump file not found: {zipFilePath}");
    }

    _logger?.LogInformation("Reading from data dump: {FilePath}...", zipFilePath);

    List<BoardGameBasic> games = [];

    using (var archive = ZipFile.OpenRead(zipFilePath))
    {
      var csvEntry = archive.GetEntry(CSV_FILE_NAME);

      if (csvEntry == null)
      {
        throw new InvalidOperationException($"CSV file '{CSV_FILE_NAME}' not found in archive.");
      }

      using var stream = csvEntry.Open();
      using var reader = new StreamReader(stream);

      // Skip header line
      reader.ReadLine();

      int lineNumber = 1;
      while (!reader.EndOfStream && games.Count < count)
      {
        var line = reader.ReadLine();
        lineNumber++;

        if (string.IsNullOrWhiteSpace(line))
          continue;

        try
        {
          var game = ParseCsvLine(line);
          if (game != null)
          {
            games.Add(game);
          }
        }
        catch (Exception ex)
        {
          _logger?.LogWarning("Failed to parse line {LineNumber}: {ErrorMessage}", lineNumber, ex.Message);
        }
      }
    }

    _logger?.LogInformation("✓ Successfully read {Count} games from data dump", games.Count);
    return games;
  }

  private BoardGameBasic? ParseCsvLine(string line)
  {
    // CSV format: id,name,yearpublished,rank,bayesaverage,average,usersrated,...
    // Handle quoted fields (names can contain commas)
    var fields = ParseCsvFields(line);

    if (fields.Count < 7)
      return null;

    try
    {
      var idString = fields[0].Trim();
      var game = new BoardGameBasic
      {
        Id = idString,
        NumId = int.Parse(idString, System.Globalization.CultureInfo.InvariantCulture),
        Name = fields[1].Trim('"'), // Remove quotes from name
        YearPublished = int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture),
        Rank = string.IsNullOrEmpty(fields[3]) ? null : int.Parse(fields[3], System.Globalization.CultureInfo.InvariantCulture),
        BayesAverage = string.IsNullOrEmpty(fields[4]) ? null : double.Parse(fields[4], System.Globalization.CultureInfo.InvariantCulture)
      };

      return game;
    }
    catch
    {
      return null;
    }
  }

  private List<string> ParseCsvFields(string line)
  {
    List<string> fields = [];
    var currentField = new System.Text.StringBuilder();
    bool inQuotes = false;

    for (int i = 0; i < line.Length; i++)
    {
      char c = line[i];

      if (c == '"')
      {
        inQuotes = !inQuotes;
        currentField.Append(c);
      }
      else if (c == ',' && !inQuotes)
      {
        fields.Add(currentField.ToString());
        currentField.Clear();
      }
      else
      {
        currentField.Append(c);
      }
    }

    // Add the last field
    fields.Add(currentField.ToString());

    return fields;
  }
}
