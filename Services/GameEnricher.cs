using System.Globalization;
using System.Xml.Linq;
using BGGDataFetcher.Models;

namespace BGGDataFetcher.Services;

public class GameEnricher
{
  public void EnrichGameFromXml(BoardGameDetailed game, XElement item)
  {
    // Get primary name (update if needed)
    var primaryName = item.Elements("name")
      .FirstOrDefault(n => n.Attribute("type")?.Value == "primary")
      ?.Attribute("value")?.Value;
    if (!string.IsNullOrEmpty(primaryName))
      game.Name = primaryName;

    // Year published
    var year = (int?)item.Element("yearpublished")?.Attribute("value");
    if (year.HasValue)
      game.YearPublished = year.Value;

    // Description
    game.Description = item.Element("description")?.Value?.Trim();

    // Min/Max Players
    game.MinPlayers = (int?)item.Element("minplayers")?.Attribute("value");
    game.MaxPlayers = (int?)item.Element("maxplayers")?.Attribute("value");

    // Playing time
    game.MinPlayTime = (int?)item.Element("minplaytime")?.Attribute("value");
    game.MaxPlayTime = (int?)item.Element("maxplaytime")?.Attribute("value");
    game.PlayingTime = (int?)item.Element("playingtime")?.Attribute("value");

    // Min Age
    game.MinAge = (int?)item.Element("minage")?.Attribute("value");

    // Categories - Extract all boardgamecategory links
    var categories = item.Elements("link")
      .Where(l => l.Attribute("type")?.Value == "boardgamecategory")
      .Select(l => l.Attribute("value")?.Value)
   .Where(v => !string.IsNullOrEmpty(v))
      .ToList();
    if (categories.Any())
      game.Categories = categories!;

    // Mechanics - Extract all boardgamemechanic links
    var mechanics = item.Elements("link")
   .Where(l => l.Attribute("type")?.Value == "boardgamemechanic")
      .Select(l => l.Attribute("value")?.Value)
      .Where(v => !string.IsNullOrEmpty(v))
      .ToList();
    if (mechanics.Any())
      game.Mechanics = mechanics!;

    // Designers - Extract all boardgamedesigner links
    var designers = item.Elements("link")
      .Where(l => l.Attribute("type")?.Value == "boardgamedesigner")
      .Select(l => l.Attribute("value")?.Value)
      .Where(v => !string.IsNullOrEmpty(v))
      .ToList();
    if (designers.Any())
      game.Designers = designers!;

    // Artists - Extract all boardgameartist links
    var artists = item.Elements("link")
      .Where(l => l.Attribute("type")?.Value == "boardgameartist")
      .Select(l => l.Attribute("value")?.Value)
      .Where(v => !string.IsNullOrEmpty(v))
    .ToList();
    if (artists.Any())
      game.Artists = artists!;

    // Publishers - Extract all boardgamepublisher links
    var publishers = item.Elements("link")
      .Where(l => l.Attribute("type")?.Value == "boardgamepublisher")
      .Select(l => l.Attribute("value")?.Value)
      .Where(v => !string.IsNullOrEmpty(v))
  .ToList();
    if (publishers.Any())
      game.Publishers = publishers!;

    // Player Count Recommendations
    ExtractPlayerCountRecommendations(game, item);

    // Statistics
    ExtractStatistics(game, item);
  }

  private void ExtractPlayerCountRecommendations(BoardGameDetailed game, XElement item)
  {
    var playerCountPoll = item.Elements("poll")
      .FirstOrDefault(p => p.Attribute("name")?.Value == "suggested_numplayers");

    if (playerCountPoll == null) return;

    // Extract detailed voting data for each player count
    var recommendations = new List<PlayerCountRecommendation>();

    foreach (var result in playerCountPoll.Elements("results"))
    {
   var numPlayersStr = result.Attribute("numplayers")?.Value;
      if (string.IsNullOrEmpty(numPlayersStr)) continue;

      // Parse numPlayers - handle "4+" by taking just the number
      if (!int.TryParse(numPlayersStr.TrimEnd('+'), out int numPlayers))
continue;

   var recommendation = new PlayerCountRecommendation
      {
   NumPlayers = numPlayers
      };

      foreach (var voteResult in result.Elements("result"))
      {
  var resultValue = voteResult.Attribute("value")?.Value;
     var numVotes = (int?)voteResult.Attribute("numvotes") ?? 0;

        if (resultValue == "Best")
          recommendation.Best = numVotes;
     else if (resultValue == "Recommended")
          recommendation.Recommended = numVotes;
        else if (resultValue == "Not Recommended")
      recommendation.NotRecommended = numVotes;
      }

      recommendations.Add(recommendation);
    }

    game.PlayerCountRecommendations = recommendations;

    // Calculate best and recommended player counts from voting data
    // Best = highest number of "Best" votes
  var bestPlayerCount = recommendations
      .OrderByDescending(r => r.Best)
 .FirstOrDefault();
    if (bestPlayerCount != null && bestPlayerCount.Best > 0)
    game.BestPlayerCount = bestPlayerCount.NumPlayers;

    // Recommended = highest combined "Best" + "Recommended" votes
    var recommendedPlayerCount = recommendations
      .OrderByDescending(r => r.Best + r.Recommended)
      .FirstOrDefault();
    if (recommendedPlayerCount != null && (recommendedPlayerCount.Best + recommendedPlayerCount.Recommended) > 0)
      game.RecommendedPlayerCount = recommendedPlayerCount.NumPlayers;
  }

  private void ExtractStatistics(BoardGameDetailed game, XElement item)
  {
    var statistics = item.Element("statistics");
 var ratings = statistics?.Element("ratings");

    if (ratings == null) return;

    // Ratings
    var usersRatedValue = ratings.Element("usersrated")?.Attribute("value")?.Value;
    if (usersRatedValue != null && int.TryParse(usersRatedValue, out int usersRated))
      game.UsersRated = usersRated;

    var averageValue = ratings.Element("average")?.Attribute("value")?.Value;
    if (averageValue != null && double.TryParse(averageValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double avg))
      game.Average = avg;

    var bayesAverageValue = ratings.Element("bayesaverage")?.Attribute("value")?.Value;
    if (bayesAverageValue != null && double.TryParse(bayesAverageValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double ba))
      game.BayesAverage = ba;

    var stdDevValue = ratings.Element("stddev")?.Attribute("value")?.Value;
    if (stdDevValue != null && double.TryParse(stdDevValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double stdDev))
      game.StandardDeviation = stdDev;

    // Rank
    var rankElement = ratings.Element("ranks")
      ?.Elements("rank")
      .FirstOrDefault(r => r.Attribute("name")?.Value == "boardgame");

    if (rankElement != null)
    {
  var rankValue = rankElement.Attribute("value")?.Value;
      if (rankValue != null && rankValue != "Not Ranked" && int.TryParse(rankValue, out int rank))
        game.Rank = rank;
    }

    // Owned/Wanting/Wishing
    var ownedValue = ratings.Element("owned")?.Attribute("value")?.Value;
    if (ownedValue != null && int.TryParse(ownedValue, out int owned))
      game.Owned = owned;

    var tradingValue = ratings.Element("trading")?.Attribute("value")?.Value;
    if (tradingValue != null && int.TryParse(tradingValue, out int trading))
      game.Trading = trading;

    var wantingValue = ratings.Element("wanting")?.Attribute("value")?.Value;
    if (wantingValue != null && int.TryParse(wantingValue, out int wanting))
      game.Wanting = wanting;

    var wishingValue = ratings.Element("wishing")?.Attribute("value")?.Value;
    if (wishingValue != null && int.TryParse(wishingValue, out int wishing))
      game.Wishing = wishing;

    var numCommentsValue = ratings.Element("numcomments")?.Attribute("value")?.Value;
    if (numCommentsValue != null && int.TryParse(numCommentsValue, out int numComments))
game.NumComments = numComments;

    var numWeightsValue = ratings.Element("numweights")?.Attribute("value")?.Value;
 if (numWeightsValue != null && int.TryParse(numWeightsValue, out int numWeights))
      game.NumWeights = numWeights;

    var averageWeightValue = ratings.Element("averageweight")?.Attribute("value")?.Value;
    if (averageWeightValue != null && double.TryParse(averageWeightValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double avgWeight))
      game.AverageWeight = avgWeight;
  }
}
