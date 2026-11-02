using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

/// <summary>Appends error lines to bgg_errors.log (and the regular log).</summary>
public class ErrorLog(ILogger<ErrorLog> logger)
{
  public const string FileName = "bgg_errors.log";
  private readonly object _lock = new();

  public void Write(string message)
  {
    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
    lock (_lock)
    {
      File.AppendAllText(FileName, line);
    }
    logger.LogError("{Message}", message);
  }
}
