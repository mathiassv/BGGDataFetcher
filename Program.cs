using System.Diagnostics;
using BGGDataFetcher.Configuration;
using BGGDataFetcher.Interfaces;
using BGGDataFetcher.Models;
using BGGDataFetcher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

// Exit codes: 0 = done, 1 = error, 2 = done but some games could not be fetched, 130 = interrupted (Ctrl+C)
const int ExitCodeOk = 0;
const int ExitCodeError = 1;
const int ExitCodeIncomplete = 2;
const int ExitCodeInterrupted = 130;

var stopwatch = Stopwatch.StartNew();

// Build configuration from JSON file and user secrets first (needed for logging setup)
const string ConfigFileName = "BGGDataFetcher.json";
var configFile = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);

var configuration = new ConfigurationBuilder()
  .SetBasePath(Directory.GetCurrentDirectory())
  .AddJsonFile("BGGDataFetcher.json", optional: false, reloadOnChange: true)
  .AddUserSecrets<Program>()
  .Build();

var loggingSettings = configuration.GetSection("Logging").Get<LoggingSettings>() ?? new LoggingSettings();
var apiSettings = configuration.GetSection("BggApi").Get<BggApiSettings>() ?? new BggApiSettings();
var settings = configuration.GetSection("Settings").Get<BGGDataFetcherSettings>() ?? new BGGDataFetcherSettings();

if (!Enum.TryParse<LogLevel>(loggingSettings.LogLevel, true, out var configuredLogLevel))
{
  Console.WriteLine($"Warning: Invalid log level '{loggingSettings.LogLevel}'. Using 'Information' instead.");
  configuredLogLevel = LogLevel.Information;
}

// Logging goes to a rolling file (logs/bgg_datafetcher_yyyyMMdd.log); the console is handled by IConsoleOutput
var serilogLogger = new LoggerConfiguration()
  .MinimumLevel.Is(ToSerilogLevel(configuredLogLevel))
  .WriteTo.File("logs/bgg_datafetcher_.log", rollingInterval: RollingInterval.Day)
  .CreateLogger();

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSerilog(serilogLogger, dispose: true));
services.AddSingleton(apiSettings);
services.AddSingleton(settings);
services.AddSingleton<IConsoleOutput, ConsoleOutput>();
services.AddSingleton<ErrorLog>();
services.AddSingleton<FileManager>();
services.AddSingleton<DataDumpReader>();
services.AddSingleton<GameEnricher>();
services.AddSingleton<XmlProcessor>();
services.AddSingleton<FetchService>();
services.AddHttpClient<BggApiClient>(client =>
{
  client.Timeout = TimeSpan.FromSeconds(60);
  client.DefaultRequestHeaders.Add("Accept", "application/xml");
  client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
  client.DefaultRequestHeaders.Authorization = new("Bearer", apiSettings.BearerToken);
})
// A run can last hours: renew pooled connections regularly so DNS / address changes are picked up
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });

await using var serviceProvider = services.BuildServiceProvider();
var output = serviceProvider.GetRequiredService<IConsoleOutput>();
var programLogger = serviceProvider.GetRequiredService<ILogger<Program>>();

output.WriteInfo("BoardGameGeek Data Fetcher - Get Game Rankings and Details");
output.WriteInfo(new string('=', 60));
output.WriteDebug("Log level set to: {0}", configuredLogLevel);

// Show where the settings were read from and what was read (the bearer token is never printed)
output.WriteLine();
output.WriteInfo("Reading settings from: {0}", configFile);
output.WriteInfo("  BggApi:");
output.WriteInfo("    BearerToken: {0}", string.IsNullOrEmpty(apiSettings.BearerToken)
  ? "NOT SET"
  : $"set (from {SourceOf("BggApi:BearerToken")})");
output.WriteInfo("  Logging:");
output.WriteInfo("    LogLevel: {0}", configuredLogLevel);
output.WriteInfo("  Settings:");
output.WriteInfo("    OutputFolder: {0}  ({1})", settings.OutputFolder, Path.GetFullPath(settings.OutputFolder));
output.WriteInfo("    LoadFromFile: {0}", settings.LoadFromFile);
output.WriteInfo("    LoadFileName: {0}", settings.LoadFileName);
output.WriteInfo("    DataDumpFileName: {0}", settings.DataDumpFileName);
output.WriteInfo("    Count: {0}", settings.Count);
output.WriteInfo("    RankedOnly: {0}", settings.RankedOnly);
output.WriteInfo("    SaveBasicFileName: {0}", settings.SaveBasicFileName);
output.WriteInfo("    SaveDetailedFileName: {0}", settings.SaveDetailedFileName);
output.WriteInfo("    FetchGameDetails: {0}", settings.FetchGameDetails);
output.WriteInfo("    RefreshAfterDays: {0}", settings.RefreshAfterDays);
output.WriteInfo("    SaveIndividualJsonFiles: {0}", settings.SaveIndividualJsonFiles);
output.WriteInfo("    IndividualJsonOutputFolder: {0}", settings.IndividualJsonOutputFolder);
output.WriteLine();

// The bearer token is only needed when calling the BGG API
if (settings.FetchGameDetails && string.IsNullOrEmpty(apiSettings.BearerToken))
{
  output.WriteError("Bearer token not found!");
  output.WriteInfo("Please set it using: dotnet user-secrets set \"BggApi:BearerToken\" \"your-token-here\"");
  output.WriteInfo("Or add it to BGGDataFetcher.json (not recommended for security)");
  return ExitCodeError;
}

// Ctrl+C stops after the current request; everything fetched so far is already saved
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
  e.Cancel = true;
  output.WriteWarning("Stopping... (progress is saved, run again to resume)");
  cts.Cancel();
};

var fetcher = serviceProvider.GetRequiredService<FetchService>();
var fileManager = serviceProvider.GetRequiredService<FileManager>();

List<BoardGameBasic> basicGames;

try
{
  basicGames = settings.LoadFromFile
    ? await fileManager.LoadBasicGamesFromJsonAsync(settings.LoadFilePath)
    : await fetcher.FetchTopGamesFromDataDumpAsync(settings.Count, settings.DataDumpFileName, settings.BasicFilePath);
}
catch (Exception ex)
{
  output.WriteError("Failed to load games: {ErrorMessage}", ex.Message);
  if (!settings.LoadFromFile)
  {
    output.WriteLine();
    output.WriteInfo("Make sure you have downloaded the BGG data dump file.");
    output.WriteInfo("See README.md for instructions on how to get the data dump.");
  }
  return ExitCodeError;
}

int exitCode = ExitCodeOk;
bool interrupted = false;

if (!settings.FetchGameDetails)
{
  output.WriteLine();
  output.WriteInfo("Skipping detailed game information fetch (as configured).");
}
else
{
  try
  {
    var result = await fetcher.FetchGameDetailsAsync(basicGames, cts.Token);

    if (result.Cancelled)
    {
      interrupted = true;
      exitCode = ExitCodeInterrupted;
    }
    else
    {
      fetcher.DisplaySummary(result);
      if (result.FailedIds.Count > 0) exitCode = ExitCodeIncomplete;
    }
  }
  catch (UnauthorizedAccessException ex)
  {
    output.WriteError(ex.Message);
    return ExitCodeError;
  }
}

stopwatch.Stop();
output.WriteLine();
if (interrupted)
{
  output.WriteWarning("Stopped before finishing - the final output was not updated. Run again to resume.");
}
else if (exitCode == ExitCodeIncomplete)
{
  output.WriteWarning("Finished, but some games could not be fetched - run again to retry them (see bgg_errors.log).");
}
else
{
  output.WriteInfo("Completed!");
}
output.WriteInfo("Total execution time: {0}", FormatElapsedTime(stopwatch.Elapsed));
programLogger.LogInformation("Total execution time: {ElapsedTime}", stopwatch.Elapsed);
return exitCode;

// Which configuration source supplied a key (the last provider that has it wins)
string SourceOf(string key)
{
  var provider = configuration.Providers.LastOrDefault(p => p.TryGet(key, out _));
  return provider switch
  {
    FileConfigurationProvider { Source.Path: ConfigFileName } => ConfigFileName,
    FileConfigurationProvider { Source.Path: "secrets.json" } => "dotnet user-secrets",
    null => "unknown",
    _ => provider.ToString() ?? "unknown"
  };
}

static LogEventLevel ToSerilogLevel(LogLevel level) => level switch
{
  LogLevel.Trace => LogEventLevel.Verbose,
  LogLevel.Debug => LogEventLevel.Debug,
  LogLevel.Information => LogEventLevel.Information,
  LogLevel.Warning => LogEventLevel.Warning,
  LogLevel.Error => LogEventLevel.Error,
  LogLevel.Critical => LogEventLevel.Fatal,
  _ => LogEventLevel.Information
};

static string FormatElapsedTime(TimeSpan elapsed)
{
  if (elapsed.TotalHours >= 1)
  {
    return $"{elapsed.Hours}h {elapsed.Minutes}m {elapsed.Seconds}s";
  }
  if (elapsed.TotalMinutes >= 1)
  {
    return $"{elapsed.Minutes}m {elapsed.Seconds}s";
  }
  return $"{elapsed.Seconds}s";
}
