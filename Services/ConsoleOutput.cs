using BGGDataFetcher.Interfaces;

namespace BGGDataFetcher.Services;

public class ConsoleOutput : IConsoleOutput
{
  public void WriteInfo(string message)
  {
    Console.WriteLine(message);
  }

  public void WriteInfo(string message, params ReadOnlySpan<object> args)
  {
    if (args.Length > 0)
    {
      // Replace structured logging placeholders {Name} with {0}, {1}, etc.
      var formattedMessage = ConvertStructuredLoggingFormat(message, args.Length);
      Console.WriteLine(formattedMessage, args.ToArray());
    }
    else
    {
      Console.WriteLine(message);
    }
  }

  public void WriteError(string message)
  {
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(message);
    Console.ResetColor();
  }

  public void WriteError(string message, params ReadOnlySpan<object> args)
  {
    Console.ForegroundColor = ConsoleColor.Red;
    if (args.Length > 0)
    {
      var formattedMessage = ConvertStructuredLoggingFormat(message, args.Length);
      Console.WriteLine(formattedMessage, args.ToArray());
    }
    else
    {
      Console.WriteLine(message);
    }
    Console.ResetColor();
  }

  public void WriteWarning(string message)
  {
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(message);
    Console.ResetColor();
  }

  public void WriteWarning(string message, params ReadOnlySpan<object> args)
  {
    Console.ForegroundColor = ConsoleColor.Yellow;
    if (args.Length > 0)
    {
      var formattedMessage = ConvertStructuredLoggingFormat(message, args.Length);
      Console.WriteLine(formattedMessage, args.ToArray());
    }
    else
    {
      Console.WriteLine(message);
    }
    Console.ResetColor();
  }

  public void WriteDebug(string message)
  {
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine(message);
    Console.ResetColor();
  }

  public void WriteDebug(string message, params ReadOnlySpan<object> args)
  {
    Console.ForegroundColor = ConsoleColor.Gray;
    if (args.Length > 0)
    {
      var formattedMessage = ConvertStructuredLoggingFormat(message, args.Length);
      Console.WriteLine(formattedMessage, args.ToArray());
    }
    else
    {
      Console.WriteLine(message);
    }
    Console.ResetColor();
  }

  public void WriteLine(string message)
  {
    Console.WriteLine(message);
  }

  public void WriteLine()
  {
    Console.WriteLine();
  }

  private static string ConvertStructuredLoggingFormat(string message, int argCount)
  {
    // Convert structured logging format {PropertyName} to Console.WriteLine format {0}, {1}, etc.
    var result = message;
    int argIndex = 0;
    int pos = 0;

    while (pos < result.Length && argIndex < argCount)
    {
      int openBrace = result.IndexOf('{', pos);
      if (openBrace == -1) break;

      int closeBrace = result.IndexOf('}', openBrace);
      if (closeBrace == -1) break;

      // Replace {PropertyName} with {argIndex}
      result = result.Substring(0, openBrace + 1) + argIndex + result.Substring(closeBrace);
      argIndex++;
      pos = openBrace + argIndex.ToString().Length + 1;
    }

    return result;
  }
}
