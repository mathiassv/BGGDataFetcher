using BGGDataFetcher.Interfaces;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

/// <summary>Writes a message to the log file and to the console in one call.</summary>
public static class LoggerExtensions
{
  public static void InfoAndShow(this ILogger logger, IConsoleOutput output, string message, params object[] args)
  {
    logger.LogInformation(message, args);
    output.WriteInfo(message, args);
  }

  public static void WarnAndShow(this ILogger logger, IConsoleOutput output, string message, params object[] args)
  {
    logger.LogWarning(message, args);
    output.WriteWarning(message, args);
  }
}
