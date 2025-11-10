using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using BGGDataFetcher.Configuration;
using BGGDataFetcher.Services;
using BGGDataFetcher.Models;
using System.Diagnostics;

// Start timing
var stopwatch = Stopwatch.StartNew();

// Build configuration from JSON file and user secrets first (needed for logging setup)
var configuration = new ConfigurationBuilder()
  .SetBasePath(Directory.GetCurrentDirectory())
  .AddJsonFile("BGGDataFetcher.json", optional: false, reloadOnChange: true)
  .AddUserSecrets<Program>()
  .Build();

// Get logging settings from configuration
var loggingSettings = new LoggingSettings();
configuration.GetSection("Logging").Bind(loggingSettings);

// Parse log level from configuration
if (!Enum.TryParse<LogLevel>(loggingSettings.LogLevel, true, out var configuredLogLevel))
{
  configuredLogLevel = LogLevel.Information; // Default fallback
  Console.WriteLine($"Warning: Invalid log level '{loggingSettings.LogLevel}'. Using 'Information' instead.");
}

// Set up dependency injection and logging
var services = new ServiceCollection();

// Configure logging to write to file instead of console
services.AddLogging(builder =>
{
  // Remove console logging for ILogger
  // builder.AddConsole();
  
  // Add file logging
  builder.AddFile("logs/bgg_datafetcher_{Date}.log", minimumLevel: configuredLogLevel);
  builder.SetMinimumLevel(configuredLogLevel);
});

var serviceProvider = services.BuildServiceProvider();
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

// Create console output wrapper (no longer needs logger)
var output = new ConsoleOutput();

output.WriteInfo("BoardGameGeek Data Fetcher - Get Game Rankings and Details");
output.WriteInfo(new string('=', 60));
output.WriteDebug("Log level set to: {0}", configuredLogLevel);

// Get API settings from configuration
var apiSettings = new BggApiSettings();
configuration.GetSection("BggApi").Bind(apiSettings);

// Get settings from configuration
var settings = new BGGDataFetcherSettings();
configuration.GetSection("Settings").Bind(settings);

// Validate settings
if (string.IsNullOrEmpty(apiSettings.BearerToken))
{
  output.WriteError("Bearer token not found!");
  output.WriteInfo("Please set it using: dotnet user-secrets set \"BggApi:BearerToken\" \"your-token-here\"");
  output.WriteInfo("Or add it to BGGDataFetcher.json (not recommended for security)");
  return;
}

// Display current configuration
output.WriteLine();
output.WriteInfo("Configuration:");
output.WriteInfo("  Log Level: {LogLevel}", configuredLogLevel);
output.WriteInfo("  Load From File: {LoadFromFile}", settings.LoadFromFile);
if (settings.LoadFromFile)
{
  output.WriteInfo("  Load File: {LoadFileName}", settings.LoadFileName);
}
else
{
  output.WriteInfo("  Data Dump File: {DataDumpFileName}", settings.DataDumpFileName);
}
output.WriteInfo("  Count: {Count}", settings.Count);
if (settings.StartPosition > 0)
{
  output.WriteInfo("  Start Position: {StartPosition} (resuming from previous run)", settings.StartPosition);
}
output.WriteInfo("  Save Basic File: {SaveBasicFileName}", settings.SaveBasicFileName);
output.WriteInfo("  Save Detailed File: {SaveDetailedFileName}", settings.SaveDetailedFileName);
output.WriteInfo("  Fetch Game Details: {FetchGameDetails}", settings.FetchGameDetails);
output.WriteLine();

var fetcherLogger = loggerFactory.CreateLogger<BGGDataFetcher.BGGDataFetcher>();
var apiLogger = loggerFactory.CreateLogger<BggApiClient>();
var dataDumpReaderLogger = loggerFactory.CreateLogger<DataDumpReader>();
var fileManagerLogger = loggerFactory.CreateLogger<FileManager>();
var fetcher = new BGGDataFetcher.BGGDataFetcher(apiSettings, fetcherLogger, apiLogger, dataDumpReaderLogger, fileManagerLogger, output);

var fileManager = new FileManager(fileManagerLogger);

List<BoardGameBasic> basicGames;

// Determine whether to use data dump or load from file
if (settings.LoadFromFile)
{
  try
  {
    basicGames = fileManager.LoadBasicGamesFromJson(settings.LoadFileName);
  }
  catch (Exception ex)
  {
    output.WriteError("Failed to load from file: {ErrorMessage}", ex.Message);
    return;
  }
}
else
{
  // Fetch from data dump (default behavior)
  try
  {
    basicGames = fetcher.FetchTopGamesFromDataDump(
      settings.Count,
      settings.DataDumpFileName,
      settings.SaveBasicFileName);
  }
  catch (Exception ex)
  {
    output.WriteError("Failed to read data dump: {ErrorMessage}", ex.Message);
    output.WriteLine();
    output.WriteInfo("Make sure you have downloaded the BGG data dump file.");
    output.WriteInfo("See README.md for instructions on how to get the data dump.");
    return;
  }
}

// Check if we should fetch details
if (!settings.FetchGameDetails)
{
  output.WriteLine();
  output.WriteInfo("Skipping detailed game information fetch (as configured).");
  output.WriteLine();
  
  // Stop timing and display elapsed time
  stopwatch.Stop();
  output.WriteInfo("Completed!");
  output.WriteInfo("Total execution time: {0}", FormatElapsedTime(stopwatch.Elapsed));

  // Also log to file
  var logger = loggerFactory.CreateLogger<Program>();
  logger.LogInformation("Total execution time: {ElapsedTime}", stopwatch.Elapsed);
  
  return;
}

// Fetch detailed information for those games
List<BoardGameDetailed> detailedGames;
if (settings.StartPosition > 0)
{
  detailedGames = await fetcher.FetchGameDetailsAsync(basicGames, settings.SaveDetailedFileName, settings.StartPosition);
}
else
{
  detailedGames = await fetcher.FetchGameDetailsAsync(basicGames, settings.SaveDetailedFileName);
}

// Display summary
fetcher.DisplaySummary(detailedGames);

// Stop timing and display elapsed time
stopwatch.Stop();

output.WriteLine();
output.WriteInfo("Completed!");
output.WriteInfo("Total execution time: {0}", FormatElapsedTime(stopwatch.Elapsed));

// Also log to file
var programLogger = loggerFactory.CreateLogger<Program>();
programLogger.LogInformation("Total execution time: {ElapsedTime}", stopwatch.Elapsed);

// Helper method to format elapsed time
static string FormatElapsedTime(TimeSpan elapsed)
{
  if (elapsed.TotalHours >= 1)
  {
    return $"{elapsed.Hours}h {elapsed.Minutes}m {elapsed.Seconds}s";
  }
  else if (elapsed.TotalMinutes >= 1)
  {
    return $"{elapsed.Minutes}m {elapsed.Seconds}s";
  }
  else
  {
    return $"{elapsed.Seconds}s";
  }
}
