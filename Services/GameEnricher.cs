using System.Globalization;
using System.Net;
using System.Xml.Linq;
using BGGDataFetcher.Models;

namespace BGGDataFetcher.Services;

public class GameEnricher
{
    // A player count needs at least this many poll votes before it is used for the best/recommended lists
    private const int MIN_VOTES_PER_COUNT = 10;

    public void EnrichGameFromXml(BoardGameDetailed game, XElement item)
    {
        var primaryName = item.Elements("name")
          .FirstOrDefault(n => n.Attribute("type")?.Value == "primary")
          ?.Attribute("value")?.Value;
        if (!string.IsNullOrEmpty(primaryName))
            game.Name = primaryName;

        var year = (int?)item.Element("yearpublished")?.Attribute("value");
        if (year.HasValue)
            game.YearPublished = year.Value;

        // BGG descriptions contain HTML entities (&mdash;, &rsquo;, &#10; ...) on top of the XML encoding
        var description = item.Element("description")?.Value;
        game.Description = description == null ? null : WebUtility.HtmlDecode(description).Trim();

        game.Thumbnail = item.Element("thumbnail")?.Value?.Trim();
        game.Image = item.Element("image")?.Value?.Trim();

        game.MinPlayers = (int?)item.Element("minplayers")?.Attribute("value");
        game.MaxPlayers = (int?)item.Element("maxplayers")?.Attribute("value");
        game.MinPlayTime = (int?)item.Element("minplaytime")?.Attribute("value");
        game.MaxPlayTime = (int?)item.Element("maxplaytime")?.Attribute("value");
        game.PlayingTime = (int?)item.Element("playingtime")?.Attribute("value");
        game.MinAge = (int?)item.Element("minage")?.Attribute("value");

        game.Categories = GetLinks(item, "boardgamecategory");
        game.Mechanics = GetLinks(item, "boardgamemechanic");
        game.Designers = GetLinks(item, "boardgamedesigner");
        game.Artists = GetLinks(item, "boardgameartist");
        game.Publishers = GetLinks(item, "boardgamepublisher");

        ExtractPlayerCountRecommendations(game, item);
        ExtractStatistics(game, item);
    }

    private static List<string> GetLinks(XElement item, string linkType) =>
        item.Elements("link")
          .Where(l => l.Attribute("type")?.Value == linkType)
          .Select(l => l.Attribute("value")?.Value)
          .OfType<string>()
          .Where(v => v.Length > 0)
          .ToList();

    private static int? ParseInt(XElement? parent, string name) =>
        int.TryParse(parent?.Element(name)?.Attribute("value")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
          ? value : null;

    private static double? ParseDouble(XElement? parent, string name) =>
        double.TryParse(parent?.Element(name)?.Attribute("value")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
          ? value : null;

    private static void ExtractPlayerCountRecommendations(BoardGameDetailed game, XElement item)
    {
        var playerCountPoll = item.Elements("poll")
          .FirstOrDefault(p => p.Attribute("name")?.Value == "suggested_numplayers");

        if (playerCountPoll == null) return;

        List<PlayerCountRecommendation> recommendations = [];

        foreach (var result in playerCountPoll.Elements("results"))
        {
            var numPlayersStr = result.Attribute("numplayers")?.Value;
            if (string.IsNullOrEmpty(numPlayersStr)) continue;

            // "4+" means "four or more"
            bool isOrMore = numPlayersStr.EndsWith('+');
            if (!int.TryParse(numPlayersStr.TrimEnd('+'), out int numPlayers))
                continue;

            var recommendation = new PlayerCountRecommendation
            {
                NumPlayers = numPlayers,
                IsOrMore = isOrMore,
                Best = 0,
                Recommended = 0,
                NotRecommended = 0
            };

            foreach (var voteResult in result.Elements("result"))
            {
                var numVotes = (int?)voteResult.Attribute("numvotes") ?? 0;

                switch (voteResult.Attribute("value")?.Value)
                {
                    case "Best": recommendation.Best = numVotes; break;
                    case "Recommended": recommendation.Recommended = numVotes; break;
                    case "Not Recommended": recommendation.NotRecommended = numVotes; break;
                }
            }

            recommendations.Add(recommendation);
        }

        game.PlayerCountRecommendations = recommendations;

        // Only trust counts the game supports (the poll also has rows beyond max players, and the "N+" row means
        // "more than max") and that enough people voted on
        var eligible = recommendations
          .Where(r => !r.IsOrMore
                      && r.TotalVotes >= MIN_VOTES_PER_COUNT
                      && (game.MinPlayers is not { } min || r.NumPlayers >= min)
                      && (game.MaxPlayers is not { } max || r.NumPlayers <= max))
          .OrderBy(r => r.NumPlayers)
          .ToList();

        game.BestPlayerCounts = eligible.Where(r => r.RecommendationLevel == "Best").Select(r => r.NumPlayers).ToList();
        game.RecommendedPlayerCounts = eligible.Where(r => r.IsPlayable).Select(r => r.NumPlayers).ToList();

        // Ties go to the lowest player count (the list is ordered by player count and OrderByDescending is stable)
        var best = eligible.OrderByDescending(r => r.Best).FirstOrDefault();
        game.BestPlayerCount = best is { Best: > 0 } ? best.NumPlayers : null;

        var recommended = eligible.Where(r => r.IsPlayable).OrderByDescending(r => r.Best + r.Recommended).FirstOrDefault();
        game.RecommendedPlayerCount = recommended?.NumPlayers;
    }

    private static void ExtractStatistics(BoardGameDetailed game, XElement item)
    {
        var ratings = item.Element("statistics")?.Element("ratings");
        if (ratings == null) return;

        game.UsersRated = ParseInt(ratings, "usersrated");
        game.Average = ParseDouble(ratings, "average");
        game.BayesAverage = ParseDouble(ratings, "bayesaverage");
        game.StandardDeviation = ParseDouble(ratings, "stddev");
        game.Owned = ParseInt(ratings, "owned");
        game.Trading = ParseInt(ratings, "trading");
        game.Wanting = ParseInt(ratings, "wanting");
        game.Wishing = ParseInt(ratings, "wishing");
        game.NumComments = ParseInt(ratings, "numcomments");
        game.NumWeights = ParseInt(ratings, "numweights");
        game.AverageWeight = ParseDouble(ratings, "averageweight");

        // "Not Ranked" simply fails to parse and leaves Rank null
        var rankValue = ratings.Element("ranks")
          ?.Elements("rank")
          .FirstOrDefault(r => r.Attribute("name")?.Value == "boardgame")
          ?.Attribute("value")?.Value;
        if (int.TryParse(rankValue, out int rank))
            game.Rank = rank;
    }
}
