using System.Globalization;
using System.Xml.Linq;
using BGGDataFetcher.Models;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

public class XmlProcessor(GameEnricher gameEnricher, ILogger<XmlProcessor> logger)
{
  /// <summary>
  /// Parses a BGG "thing" response and extracts the games.
  /// Returns null if the XML itself cannot be parsed; games that fail individually are reported via <paramref name="onItemError"/>.
  /// </summary>
  public List<BoardGameDetailed>? TryProcessXmlGames(string xmlContent, Action<string> onItemError)
  {
    XDocument xml;
    try
    {
      xml = XDocument.Parse(xmlContent);
    }
    catch (Exception ex)
    {
      logger.LogWarning("Could not parse BGG XML response: {ErrorMessage}", ex.Message);
      return null;
    }

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
          NumId = int.Parse(id, CultureInfo.InvariantCulture),
          Name = string.Empty,
          YearPublished = 0,
          Categories = [],
          Mechanics = [],
          Designers = [],
          Artists = [],
          Publishers = [],
          PlayerCountRecommendations = []
        };
        gameEnricher.EnrichGameFromXml(game, item);
        games.Add(game);
      }
      catch (Exception ex)
      {
        onItemError($"Failed to enrich game ID {id}: {ex.Message}");
      }
    }

    return games;
  }
}
