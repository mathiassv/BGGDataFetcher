using System.Text.Json;
using BGGDataFetcher.Models;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

public class FileManager
{
  private readonly ILogger<FileManager>? _logger;

  public FileManager(ILogger<FileManager>? logger = null)
  {
    _logger = logger;
  }

  public void SaveBasicGamesToJson(List<BoardGameBasic> games, string fileName)
  {
    var options = new JsonSerializerOptions
    {
      WriteIndented = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    var json = JsonSerializer.Serialize(games.OrderBy(g => g.Rank ?? int.MaxValue).ToList(), options);
    File.WriteAllText(fileName, json);

    _logger?.LogInformation("✓ Data saved to: {FileName}", fileName);
  }

  public List<BoardGameBasic> LoadBasicGamesFromJson(string fileName)
  {
    if (!File.Exists(fileName))
    {
      throw new FileNotFoundException($"File not found: {fileName}");
    }

    var json = File.ReadAllText(fileName);
    var options = new JsonSerializerOptions
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    var games = JsonSerializer.Deserialize<List<BoardGameBasic>>(json, options);

    if (games == null)
    {
      throw new InvalidOperationException($"Failed to deserialize games from {fileName}");
    }

    _logger?.LogInformation("✓ Loaded {Count} games from: {FileName}", games.Count, fileName);
    return games;
  }

  public void SaveDetailedGamesToJson(List<BoardGameDetailed> games, string fileName)
  {
    var options = new JsonSerializerOptions
    {
      WriteIndented = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    var json = JsonSerializer.Serialize(games.OrderBy(g => g.Rank ?? int.MaxValue).ToList(), options);
    File.WriteAllText(fileName, json);

    _logger?.LogInformation("✓ Data saved to: {FileName}", fileName);
  }
}
