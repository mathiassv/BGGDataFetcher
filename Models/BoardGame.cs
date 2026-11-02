namespace BGGDataFetcher.Models;

/// <summary>
/// Detailed board game information obtained from the BGG API.
/// Extends the basic data-dump information with everything the API provides.
/// </summary>
public class BoardGameDetailed : BoardGameBasic
{
  public string? Description { get; set; }
  public string? Thumbnail { get; set; }
  public string? Image { get; set; }
  public int? MinPlayers { get; set; }
  public int? MaxPlayers { get; set; }
  public int? MinPlayTime { get; set; }
  public int? MaxPlayTime { get; set; }
  public int? PlayingTime { get; set; }
  public int? MinAge { get; set; }
  public double? StandardDeviation { get; set; }
  public int? Owned { get; set; }
  public int? Trading { get; set; }
  public int? Wanting { get; set; }
  public int? Wishing { get; set; }
  public int? NumComments { get; set; }
  public int? NumWeights { get; set; }
  public double? AverageWeight { get; set; }

  // Categories and Mechanics
  public required List<string> Categories { get; set; } = [];
  public required List<string> Mechanics { get; set; } = [];

  // Credits
  public required List<string> Designers { get; set; } = [];
  public required List<string> Artists { get; set; } = [];
  public required List<string> Publishers { get; set; } = [];

  // Player Count Recommendations - Summary.
  // The lists only contain counts inside the game's min/max players that have enough poll votes.
  // BestPlayerCounts: counts voters call "Best". RecommendedPlayerCounts: all playable counts (including the best ones).
  public List<int> BestPlayerCounts { get; set; } = [];
  public List<int> RecommendedPlayerCounts { get; set; } = [];

  // The single count with the most "Best" votes / the most positive (Best + Recommended) votes
  public int? BestPlayerCount { get; set; }
  public int? RecommendedPlayerCount { get; set; }

  // Player Count Recommendations - Detailed voting data
  public required List<PlayerCountRecommendation> PlayerCountRecommendations { get; set; } = [];

  /// <summary>When the details were fetched from the BGG API (UTC). Used to decide what to refresh.</summary>
  public DateTime? FetchedAt { get; set; }

  /// <summary>
  /// Applies the data-dump values. The ranking snapshot (position, ranks, ratings) always comes from the
  /// dump, so applying a newer dump to a previously fetched game updates its ranking without fetching it again.
  /// Name, year and id are only filled in when the API did not provide them.
  /// </summary>
  public void ApplyDumpData(BoardGameBasic basic)
  {
    if (string.IsNullOrEmpty(Name)) Name = basic.Name;
    if (YearPublished == 0) YearPublished = basic.YearPublished;
    if (NumId == 0) NumId = basic.NumId;

    RankPos = basic.RankPos;
    IsExpansion = basic.IsExpansion;
    Rank = basic.Rank;
    BayesAverage = basic.BayesAverage;
    Average = basic.Average;
    UsersRated = basic.UsersRated;
    AbstractsRank = basic.AbstractsRank;
    CgsRank = basic.CgsRank;
    ChildrensGamesRank = basic.ChildrensGamesRank;
    FamilyGamesRank = basic.FamilyGamesRank;
    PartyGamesRank = basic.PartyGamesRank;
    StrategyGamesRank = basic.StrategyGamesRank;
    ThematicRank = basic.ThematicRank;
    WarGamesRank = basic.WarGamesRank;
  }
}
