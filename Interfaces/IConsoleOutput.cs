namespace BGGDataFetcher.Interfaces;

public interface IConsoleOutput
{
  void WriteInfo(string message);
  void WriteInfo(string message, params object[] args);
  void WriteError(string message);
  void WriteError(string message, params object[] args);
  void WriteWarning(string message);
  void WriteWarning(string message, params object[] args);
  void WriteDebug(string message);
  void WriteDebug(string message, params object[] args);
  void WriteLine(string message);
  void WriteLine();
}
