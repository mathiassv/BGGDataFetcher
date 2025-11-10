using System.Xml.Linq;
using BGGDataFetcher.Models;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

public class XmlProcessor
{
  private readonly GameEnricher _gameEnricher;
  private readonly ILogger? _logger;

  public XmlProcessor(ILogger? logger = null)
  {
    _gameEnricher = new GameEnricher();
    _logger = logger;
  }

  /// <summary>
  /// Parses XML content and returns an XDocument
  /// </summary>
  public XDocument ParseXml(string xmlContent)
  {
    return XDocument.Parse(xmlContent);
  }

  /// <summary>
  /// Processes XML document and extracts game details
  /// </summary>
  public List<BoardGameDetailed> ProcessXmlGames(XDocument xml, Action<string> logError, Action<string, object[]> logWarning)
  {
    var games = new List<BoardGameDetailed>();

    foreach (var item in xml.Root?.Elements("item") ?? [])
    {
      var id = (int?)item.Attribute("id");
      if (id == null) continue;

      try
      {
        var game = new BoardGameDetailed { Id = id.Value };
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
  /// Attempts to parse XML and process games, returns null if parsing fails
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
}
