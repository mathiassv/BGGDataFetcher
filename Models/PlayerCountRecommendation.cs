namespace BGGDataFetcher.Models;

public class PlayerCountRecommendation
{
  public required int NumPlayers { get; set; }
  public required int Best { get; set; }
  public required int Recommended { get; set; }
  public required int NotRecommended { get; set; }
}
