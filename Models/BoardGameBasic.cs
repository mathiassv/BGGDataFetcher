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
  public int? Rank { get; set; }
  public double? BayesAverage { get; set; }
}
