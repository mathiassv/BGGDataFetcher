namespace BGGDataFetcher.Models;

/// <summary>
/// Detailed board game information obtained from BGG API
/// Contains all basic and detailed information
/// </summary>
public class BoardGameDetailed
{
  // Basic properties (from BoardGameBasic)
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public int YearPublished { get; set; }
  public int? Rank { get; set; }
  public double? BayesAverage { get; set; }

  // Detailed properties
  public string? Description { get; set; }
  public int? MinPlayers { get; set; }
  public int? MaxPlayers { get; set; }
  public int? MinPlayTime { get; set; }
  public int? MaxPlayTime { get; set; }
  public int? PlayingTime { get; set; }
  public int? MinAge { get; set; }
  public int? UsersRated { get; set; }
  public double? Average { get; set; }
  public double? StandardDeviation { get; set; }
  public int? Owned { get; set; }
  public int? Trading { get; set; }
  public int? Wanting { get; set; }
  public int? Wishing { get; set; }
  public int? NumComments { get; set; }
  public int? NumWeights { get; set; }
  public double? AverageWeight { get; set; }

  // Categories and Mechanics
  public List<string> Categories { get; set; } = new List<string>();
  public List<string> Mechanics { get; set; } = new List<string>();

  // Credits
  public List<string> Designers { get; set; } = new List<string>();
  public List<string> Artists { get; set; } = new List<string>();
  public List<string> Publishers { get; set; } = new List<string>();

  // Player Count Recommendations - Summary
  public int? BestPlayerCount { get; set; }
  public int? RecommendedPlayerCount { get; set; }

  // Player Count Recommendations - Detailed voting data
  public List<PlayerCountRecommendation> PlayerCountRecommendations { get; set; } = new List<PlayerCountRecommendation>();
}
