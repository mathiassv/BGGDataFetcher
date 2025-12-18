namespace BGGDataFetcher.Models;

public class PlayerCountRecommendation
{
  public required int NumPlayers { get; set; }
  public bool IsOrMore { get; set; }
  public required int Best { get; set; }
  public required int Recommended { get; set; }
  public required int NotRecommended { get; set; }

  public string RecommendationLevel
  {
    get
    {
      if (Best >= Recommended && Best >= NotRecommended)
        return "Best";
      if (Recommended >= NotRecommended)
        return "Recommended";
      return "Not Recommended";
    }
  }
}
