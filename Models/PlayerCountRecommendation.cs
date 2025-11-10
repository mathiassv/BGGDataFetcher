namespace BGGDataFetcher.Models;

public class PlayerCountRecommendation
{
  public int NumPlayers { get; set; }
  public int Best { get; set; }
  public int Recommended { get; set; }
  public int NotRecommended { get; set; }
}
