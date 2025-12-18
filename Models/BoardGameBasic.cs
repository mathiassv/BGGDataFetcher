namespace BGGDataFetcher.Models;

/// <summary>
/// Basic board game information obtained from web scraping
/// </summary>
public class BoardGameBasic
{
  public required string Id { get; set; }
  public required int NumId { get; set; }
  public required string Name { get; set; }
  public required int YearPublished { get; set; }
  public int RankPos { get; set; }
  public int? Rank { get; set; }
  public double? BayesAverage { get; set; }
  public double? Average { get; set; }
  public int? UsersRated { get; set; }
  public bool IsExpansion { get; set; }
  public int? AbstractsRank { get; set; }
  public int? CgsRank { get; set; }
  public int? ChildrensGamesRank { get; set; }
  public int? FamilyGamesRank { get; set; }
  public int? PartyGamesRank { get; set; }
  public int? StrategyGamesRank { get; set; }
  public int? ThematicRank { get; set; }
  public int? WarGamesRank { get; set; }
}
