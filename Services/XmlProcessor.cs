using System.Xml.Linq;
using BGGDataFetcher.Models;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

public class XmlProcessor(ILogger? logger = null)
{
  private readonly GameEnricher _gameEnricher = new();
  private readonly ILogger? _logger = logger;

  /// <summary>
  /// Parses XML content and returns an XDocument
  /// </summary>
  public XDocument ParseXml(string xmlContent)
  {
    return XDocument.Parse(xmlContent);
  }

  /// <summary>
  /// Processes XML document and extracts game details (synchronous version)
  /// </summary>
  public List<BoardGameDetailed> ProcessXmlGames(XDocument xml, Action<string> logError, Action<string, object[]> logWarning)
  {
    List<BoardGameDetailed> games = [];

    foreach (var item in xml.Root?.Elements("item") ?? [])
    {
      var id = item.Attribute("id")?.Value;
      if (string.IsNullOrEmpty(id)) continue;

      try
      {
        var game = new BoardGameDetailed 
        { 
          Id = id,
          NumId = int.Parse(id, System.Globalization.CultureInfo.InvariantCulture),
          Name = string.Empty,
          YearPublished = 0,
          Categories = [],
          Mechanics = [],
          Designers = [],
          Artists = [],
          Publishers = [],
          PlayerCountRecommendations = []
        };
        _gameEnricher.EnrichGameFromXml(game, item);
        games.Add(game);
      }
      catch (Exception ex)
      {
        logError($"Failed to enrich game ID {id}: {ex.Message}");
        logWarning("Failed to process game ID {GameId}: {ErrorMessage}", [id, ex.Message]);
      }
    }

    return games;
  }

  /// <summary>
  /// Processes XML document and extracts game details (async version)
  /// </summary>
  public async Task<List<BoardGameDetailed>> ProcessXmlGamesAsync(XDocument xml, Func<string, Task> logErrorAsync, Action<string, object[]> logWarning)
  {
    List<BoardGameDetailed> games = [];

    foreach (var item in xml.Root?.Elements("item") ?? [])
    {
      var id = item.Attribute("id")?.Value;
      if (string.IsNullOrEmpty(id)) continue;

      try
      {
        var game = new BoardGameDetailed 
        { 
          Id = id,
          NumId = int.Parse(id, System.Globalization.CultureInfo.InvariantCulture),
          Name = string.Empty,
          YearPublished = 0,
          Categories = [],
          Mechanics = [],
          Designers = [],
          Artists = [],
          Publishers = [],
          PlayerCountRecommendations = []
        };
        _gameEnricher.EnrichGameFromXml(game, item);
        games.Add(game);
      }
      catch (Exception ex)
      {
        await logErrorAsync($"Failed to enrich game ID {id}: {ex.Message}");
        logWarning("Failed to process game ID {GameId}: {ErrorMessage}", [id, ex.Message]);
      }
    }

    return games;
  }

  /// <summary>
  /// Attempts to parse XML and process games, returns null if parsing fails (synchronous version)
  /// </summary>
  public List<BoardGameDetailed>? TryProcessXmlGames(string xmlContent, Action<string> logError, Action<string, object[]> logWarning)
  {
    try
    {
      var xml = ParseXml(xmlContent);
      return ProcessXmlGames(xml, logError, logWarning);
    }
    catch (Exception)
    {
      // Return null to indicate parsing failure
      return null;
    }
  }

  /// <summary>
  /// Attempts to parse XML and process games, returns null if parsing fails (async version)
  /// </summary>
  public async Task<List<BoardGameDetailed>?> TryProcessXmlGamesAsync(string xmlContent, Func<string, Task> logErrorAsync, Action<string, object[]> logWarning)
  {
    try
    {
      var xml = ParseXml(xmlContent);
      return await ProcessXmlGamesAsync(xml, logErrorAsync, logWarning);
    }
    catch (Exception)
    {
      // Return null to indicate parsing failure
      return null;
    }
  }
}
