namespace BGGDataFetcher.Models;

/// <summary>
/// Basic board game information obtained from web scraping
/// </summary>
public class BoardGameBasic
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public int YearPublished { get; set; }
  public int? Rank { get; set; }
  public double? BayesAverage { get; set; }
}
