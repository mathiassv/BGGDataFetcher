using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using BGGDataFetcher.Models;
using Microsoft.Extensions.Logging;

namespace BGGDataFetcher.Services;

public class FileManager(ILogger<FileManager> logger)
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Creates the folder of a file path if it does not exist yet.</summary>
    private static void EnsureDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }

    public async Task SaveBasicGamesToJsonAsync(List<BoardGameBasic> games, string fileName)
    {
        EnsureDirectory(fileName);
        await using var stream = File.Create(fileName);
        await JsonSerializer.SerializeAsync(stream, games, IndentedOptions);

        logger.LogInformation("✓ Data saved to: {FileName}", fileName);
    }

    public async Task<List<BoardGameBasic>> LoadBasicGamesFromJsonAsync(string fileName)
    {
        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException($"File not found: {fileName}");
        }

        await using var stream = File.OpenRead(fileName);
        var games = await JsonSerializer.DeserializeAsync<List<BoardGameBasic>>(stream, CompactOptions) ?? [];

        logger.LogInformation("✓ Loaded {Count} games from: {FileName}", games.Count, fileName);
        return games;
    }

    #region Detailed games (JSON-lines file: one compact JSON object per line)

    private record IdStamp(string? Id, DateTime? FetchedAt);

    /// <summary>Where the newest record of a game is stored in the file.</summary>
    private readonly record struct IndexEntry(long Offset, int Length, DateTime? FetchedAt);

    /// <summary>
    /// Appends games to the file. Appending a batch is cheap and a crash can at worst leave one torn last line.
    /// </summary>
    public async Task AppendDetailedGamesAsync(string jsonlPath, IEnumerable<BoardGameDetailed> games)
    {
        var lines = string.Concat(games.Select(g => JsonSerializer.Serialize(g, CompactOptions) + "\n"));
        if (lines.Length == 0) return;

        EnsureDirectory(jsonlPath);
        await using var stream = new FileStream(jsonlPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

        // If a previous run was killed mid-write, terminate the torn line so it does not swallow the next record
        if (stream.Length > 0)
        {
            stream.Seek(-1, SeekOrigin.End);
            if (stream.ReadByte() != '\n')
            {
                stream.Seek(0, SeekOrigin.End);
                stream.WriteByte((byte)'\n');
            }
        }

        stream.Seek(0, SeekOrigin.End);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(lines);
    }

    /// <summary>
    /// Scans the file once (reading only id and timestamp of each line) and returns where the newest record of
    /// every game is. A game appears more than once after a refresh; the last record wins. Unreadable lines are skipped.
    /// </summary>
    private Dictionary<string, IndexEntry> IndexJsonl(string jsonlPath)
    {
        Dictionary<string, IndexEntry> index = [];
        if (!File.Exists(jsonlPath)) return index;

        using var stream = new FileStream(jsonlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
        var buffer = new byte[1 << 16];
        using var line = new MemoryStream();
        long chunkStart = 0, lineStart = 0;
        int lineNumber = 0, read;

        void ProcessLine()
        {
            lineNumber++;
            if (line.Length == 0) return;

            IdStamp? stamp = null;
            try { stamp = JsonSerializer.Deserialize<IdStamp>(line.GetBuffer().AsSpan(0, (int)line.Length), CompactOptions); }
            catch (JsonException) { }

            if (stamp?.Id == null)
            {
                logger.LogWarning("Skipping unreadable line {LineNumber} in {FileName}", lineNumber, jsonlPath);
                return;
            }

            index[stamp.Id] = new IndexEntry(lineStart, (int)line.Length, stamp.FetchedAt);
        }

        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            int start = 0;
            while (start < read)
            {
                int newline = Array.IndexOf(buffer, (byte)'\n', start, read - start);
                if (newline < 0)
                {
                    line.Write(buffer, start, read - start);
                    break;
                }

                line.Write(buffer, start, newline - start);
                ProcessLine();
                line.SetLength(0);
                start = newline + 1;
                lineStart = chunkStart + start;
            }
            chunkStart += read;
        }

        ProcessLine(); // last line without a newline (a torn write)
        return index;
    }

    /// <summary>When each game in the file was last fetched (null if the record has no timestamp).</summary>
    public Dictionary<string, DateTime?> GetFetchTimestamps(string jsonlPath) =>
        IndexJsonl(jsonlPath).ToDictionary(kv => kv.Key, kv => kv.Value.FetchedAt);

    /// <summary>
    /// Rewrites the file so it holds exactly one record per game, with the games of <paramref name="orderedGames"/>
    /// first in that order (rank order) and <paramref name="apply"/> run on each of them. Records that are not in the
    /// list (e.g. after lowering Count) are kept unchanged at the end. Outdated and torn records are dropped.
    /// </summary>
    public void RewriteDetailedFile(string jsonlPath, IReadOnlyList<BoardGameBasic> orderedGames, Action<BoardGameDetailed, BoardGameBasic> apply)
    {
        if (!File.Exists(jsonlPath)) return;

        var index = IndexJsonl(jsonlPath);
        var tempFile = jsonlPath + ".tmp";
        HashSet<string> written = [];

        using (var input = new FileStream(jsonlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16))
        using (var output = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
        {
            foreach (var basic in orderedGames)
            {
                if (!index.TryGetValue(basic.Id, out var entry) || !written.Add(basic.Id)) continue;

                var game = Deserialize(ReadLine(input, entry));
                if (game == null)
                {
                    logger.LogWarning("Dropping unreadable record of game {GameId}", basic.Id);
                    continue;
                }

                apply(game, basic);
                output.Write(JsonSerializer.SerializeToUtf8Bytes(game, CompactOptions));
                output.WriteByte((byte)'\n');
            }

            foreach (var (id, entry) in index.Where(kv => !written.Contains(kv.Key)).OrderBy(kv => kv.Value.Offset))
            {
                output.Write(ReadLine(input, entry));
                output.WriteByte((byte)'\n');
            }
        }

        File.Move(tempFile, jsonlPath, overwrite: true);
    }

    private static byte[] ReadLine(FileStream input, IndexEntry entry)
    {
        var bytes = new byte[entry.Length];
        input.Seek(entry.Offset, SeekOrigin.Begin);
        input.ReadExactly(bytes);
        return bytes;
    }

    private static BoardGameDetailed? Deserialize(ReadOnlySpan<byte> json)
    {
        try
        {
            return JsonSerializer.Deserialize<BoardGameDetailed>(json, CompactOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Streams the games of a file that is already clean (one record per game), e.g. after
    /// <see cref="RewriteDetailedFile"/>. Unreadable lines are skipped with a warning.
    /// </summary>
    public IEnumerable<BoardGameDetailed> ReadDetailedGames(string jsonlPath)
    {
        if (!File.Exists(jsonlPath)) yield break;

        int lineNumber = 0;
        foreach (var line in File.ReadLines(jsonlPath))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var game = Deserialize(Encoding.UTF8.GetBytes(line));
            if (game == null)
            {
                logger.LogWarning("Skipping unreadable line {LineNumber} in {FileName}", lineNumber, jsonlPath);
                continue;
            }

            yield return game;
        }
    }

    #endregion

    /// <summary>Writes the games as an indented JSON array without holding them all in memory.</summary>
    public async Task ExportJsonArrayAsync(IEnumerable<BoardGameDetailed> games, string fileName)
    {
        // Write to a temp file first so an interrupted export never destroys the previous output
        EnsureDirectory(fileName);
        var tempFile = fileName + ".tmp";
        int count = 0;

        await using (var writer = new StreamWriter(tempFile, false, new UTF8Encoding(false)))
        {
            await writer.WriteAsync("[");
            foreach (var game in games)
            {
                var json = JsonSerializer.Serialize(game, IndentedOptions).Replace("\n", "\n  ");
                await writer.WriteAsync((count == 0 ? "\n  " : ",\n  ") + json);
                count++;
            }
            await writer.WriteAsync(count == 0 ? "]\n" : "\n]\n");
        }

        File.Move(tempFile, fileName, overwrite: true);
        logger.LogInformation("✓ {Count} games saved to: {FileName}", count, fileName);
    }

    public async Task SaveIndividualGameJsonFilesAsync(IEnumerable<BoardGameDetailed> games, string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);

        int savedCount = 0;
        logger.LogInformation("Saving individual game files to {OutputFolder}...", outputFolder);

        foreach (var game in games)
        {
            try
            {
                // The BGG id is stable across data dumps (rank is not), so an update overwrites the same file
                var filePath = Path.Combine(outputFolder, $"{game.Id}.json");

                await using var stream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(stream, game, IndentedOptions);
                savedCount++;

                if (savedCount % 1000 == 0)
                {
                    logger.LogInformation("  Progress: {SavedCount} files saved", savedCount);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to save individual file for game {GameId} ({GameName}): {ErrorMessage}",
                  game.Id, game.Name, ex.Message);
            }
        }

        logger.LogInformation("✓ Saved {SavedCount} individual game files to: {OutputFolder}", savedCount, outputFolder);
    }
}
