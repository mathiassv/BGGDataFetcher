using System.Text.Json.Serialization;

namespace BGGDataFetcher.Models;

/// <summary>
/// Community votes for one player count from the BGG "suggested number of players" poll.
/// </summary>
/// <remarks>
/// <see cref="IsOrMore"/> marks BGG's extra "N+" row, which is the vote for playing with more than the
/// game's maximum player count. It is stored for completeness but never counted as a supported player count.
/// </remarks>
public class PlayerCountRecommendation
{
  public int NumPlayers { get; set; }
  public bool IsOrMore { get; set; }
  public int Best { get; set; }
  public int Recommended { get; set; }
  public int NotRecommended { get; set; }

  // The properties below are derived from the votes and therefore not written to the JSON output

  [JsonIgnore]
  public int TotalVotes => Best + Recommended + NotRecommended;

  /// <summary>Playable = more voters call it Best/Recommended than Not Recommended.</summary>
  [JsonIgnore]
  public bool IsPlayable => Best + Recommended > NotRecommended;

  /// <summary>"Best", "Recommended", "Not Recommended" or "No votes".</summary>
  [JsonIgnore]
  public string RecommendationLevel
  {
    get
    {
      if (TotalVotes == 0)
        return "No votes";
      if (!IsPlayable)
        return "Not Recommended";
      return Best > 0 && Best >= Recommended ? "Best" : "Recommended";
    }
  }
}
