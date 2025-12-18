using System.Text.Json;
using BGGDataFetcher.Models;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

public class FileManager(ILogger<FileManager>? logger = null)
{
    private readonly ILogger<FileManager>? _logger = logger;

    public async Task SaveBasicGamesToJsonAsync(List<BoardGameBasic> games, string fileName)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await using var stream = File.Create(fileName);
        await JsonSerializer.SerializeAsync(stream, games.OrderBy(g => g.RankPos).ToList(), options);

        _logger?.LogInformation("✓ Data saved to: {FileName}", fileName);
    }

    public async Task<List<BoardGameBasic>> LoadBasicGamesFromJsonAsync(string fileName)
    {
        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException($"File not found: {fileName}");
        }

        await using var stream = File.OpenRead(fileName);
        var games = await JsonSerializer.DeserializeAsync<List<BoardGameBasic>>(stream, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) ?? [];

        _logger?.LogInformation("✓ Loaded {Count} games from: {FileName}", games.Count, fileName);
        return games;
    }

    public async Task SaveDetailedGamesToJsonAsync(List<BoardGameDetailed> games, string fileName)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await using var stream = File.Create(fileName);
        await JsonSerializer.SerializeAsync(stream, games.OrderBy(g => g.RankPos).ToList(), options);

        _logger?.LogInformation("✓ Data saved to: {FileName}", fileName);
    }

    public async Task SaveIndividualGameJsonFilesAsync(List<BoardGameDetailed> games, string outputFolder)
    {
        // Create output directory if it doesn't exist
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            _logger?.LogInformation("Created output folder: {OutputFolder}", outputFolder);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        int savedCount = 0;
        int totalCount = games.Count;
        const int LargeBatchThreshold = 1000;
        const int MediumBatchThreshold = 100;
        const int ProgressIntervalLarge = 100;
        const int ProgressIntervalMedium = 10;
        const int ProgressIntervalSmall = 1;

        int progressInterval = totalCount >= LargeBatchThreshold
            ? ProgressIntervalLarge
            : totalCount >= MediumBatchThreshold
                ? ProgressIntervalMedium
                : ProgressIntervalSmall;

        _logger?.LogInformation("Saving {TotalCount} individual game files...", totalCount);

        foreach (var game in games)
        {
            try
            {
                // Create a safe filename from the game ID and name
                var safeFileName = $"{game.Id}.json";
                var filePath = Path.Combine(outputFolder, safeFileName);

                await using var stream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(stream, game, options);
                savedCount++;

                // Log progress at intervals
                if (savedCount % progressInterval == 0 || savedCount == totalCount)
                {
                    _logger?.LogInformation("  Progress: {SavedCount}/{TotalCount} files saved ({Percentage:F1}%)",
                        savedCount, totalCount, (savedCount * 100.0) / totalCount);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Failed to save individual file for game {GameId} ({GameName}): {ErrorMessage}",
                  game.Id, game.Name, ex.Message);
            }
        }

        _logger?.LogInformation("✓ Saved {SavedCount} individual game files to: {OutputFolder}", savedCount, outputFolder);
    }
}
