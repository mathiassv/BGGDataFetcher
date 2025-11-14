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
        await JsonSerializer.SerializeAsync(stream, games.OrderBy(g => g.Rank ?? int.MaxValue).ToList(), options);

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
        await JsonSerializer.SerializeAsync(stream, games.OrderBy(g => g.Rank ?? int.MaxValue).ToList(), options);

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
