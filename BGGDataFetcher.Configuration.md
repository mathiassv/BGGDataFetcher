# BGGDataFetcher Configuration Examples

This file contains example configurations for `BGGDataFetcher.json`.

## Default Configuration
Use data dump to get top 100 games and fetch details:
```json
{
  "BggApi": {
  "BearerToken": ""
  },
  "Logging": {
    "LogLevel": "Information"
  },
  "Settings": {
    "LoadFromFile": false,
    "DataDumpFileName": "boardgames_ranks_2025-11-07.zip",
    "Count": 100,
    "LoadFileName": "TopGames.json",
    "SaveBasicFileName": "TopGames.json",
    "SaveDetailedFileName": "TopGamesDetailed.json",
    "FetchGameDetails": true
  }
}
```

## Verbose Logging (Debug Mode)
See detailed diagnostic information:
```json
{
  "Logging": {
    "LogLevel": "Debug"
  },
  "Settings": {
    "Count": 100,
    "FetchGameDetails": true
  }
}
```

## Minimal Logging (Warnings and Errors Only)
Reduce console output to only important messages:
```json
{
  "Logging": {
    "LogLevel": "Warning"
  },
  "Settings": {
    "Count": 1000,
    "FetchGameDetails": true
  }
}
```

## Top 500 Games with Details
```json
{
  "Settings": {
    "DataDumpFileName": "boardgames_ranks_2025-11-07.zip",
    "Count": 500,
  "FetchGameDetails": true
  }
}
```

## Top 1000 Games, Skip Details (Fast)
```json
{
  "Settings": {
 "DataDumpFileName": "boardgames_ranks_2025-11-07.zip",
    "Count": 1000,
    "FetchGameDetails": false
  }
}
```

## Custom Output Files
```json
{
  "Settings": {
    "DataDumpFileName": "boardgames_ranks_2025-11-07.zip",
    "Count": 5000,
    "SaveBasicFileName": "Top5000.json",
    "SaveDetailedFileName": "Top5000Detailed.json",
    "FetchGameDetails": true
  }
}
```

## Use Different Data Dump File
```json
{
  "Settings": {
    "DataDumpFileName": "boardgames_ranks_2025-12-01.zip",
    "Count": 250,
    "SaveBasicFileName": "MyTop250.json",
"SaveDetailedFileName": "MyTop250Detailed.json",
    "FetchGameDetails": true
  }
}
```

## Load from File and Fetch Details
```json
{
  "Settings": {
  "LoadFromFile": true,
 "LoadFileName": "TopGames.json",
    "SaveDetailedFileName": "TopGamesDetailed.json",
    "FetchGameDetails": true
  }
}
```

## Load from Custom File
```json
{
  "Settings": {
    "LoadFromFile": true,
    "LoadFileName": "MyGames.json",
    "SaveDetailedFileName": "MyGamesDetailed.json",
  "FetchGameDetails": true
  }
}
```

## Load from File, Skip Details
```json
{
  "Settings": {
    "LoadFromFile": true,
    "LoadFileName": "TopGames.json",
    "FetchGameDetails": false
  }
}
```

## Configuration Options

### Logging Section

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LogLevel` | string | `"Information"` | Logging verbosity: `"Trace"`, `"Debug"`, `"Information"`, `"Warning"`, `"Error"`, `"Critical"`, or `"None"` |

**Log Level Descriptions:**
- **Trace**: Most verbose - all diagnostic details
- **Debug**: Detailed diagnostics including delays and verbose output
- **Information**: Normal operation messages (default, recommended)
- **Warning**: Non-fatal issues only
- **Error**: Fatal errors only
- **Critical**: Critical failures only
- **None**: No logging output

### Settings Section

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LoadFromFile` | bool | `false` | If `true`, load games from previously saved JSON file; if `false`, use data dump |
| `DataDumpFileName` | string | `"boardgames_ranks_2025-11-07.zip"` | Path to BGG data dump ZIP file (used when LoadFromFile is false) |
| `Count` | int | `100` | Number of top games to fetch |
| `StartPosition` | int | `0` | Start position for fetching details (0-based index, used for resuming interrupted runs) |
| `LoadFileName` | string | `"TopGames.json"` | JSON file to load games from (only used when LoadFromFile is true) |
| `SaveBasicFileName` | string | `"TopGames.json"` | JSON file to save basic game data to |
| `SaveDetailedFileName` | string | `"TopGamesDetailed.json"` | JSON file to save detailed game data to |
| `FetchGameDetails` | bool | `true` | If `true`, fetch detailed game information from the API; if `false`, skip details |

### BggApi Section

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BearerToken` | string | `""` | BGG API Bearer Token (recommended to use User Secrets instead) |

## Logging Options

### Logging Section

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LogLevel` | string | `"Information"` | Set the level of logging: `Trace`, `Debug`, `Information`, `Warning`, `Error`, or `Critical` |

## BGG Data Dump

BGG provides periodic data dumps of their game rankings at: https://boardgamegeek.com/data_dumps/bg_ranks

**Important**: You must be logged in to BoardGameGeek to access and download the data dump files.

### Benefits of Using Data Dump:
- ? **Extremely fast** - Import thousands of games in seconds
- ?? **Respectful** - Uses BGG's official export, no server load
- ?? **Complete data** - Get all ranked games instantly
- ? **Reliable** - No risk of being blocked or rate-limited
- ?? **Up-to-date** - BGG provides regular updates

### How to Get the Data Dump:

1. **Log in to BoardGameGeek**
   - Visit https://boardgamegeek.com and log in
   - You need a free account to access data dumps

2. **Download the latest dump**
   - Visit https://boardgamegeek.com/data_dumps/bg_ranks
   - Click the download link for the latest ZIP file
   - File is typically 5-10 MB

3. **Place in project directory**
   - Save the ZIP file in your BGGDataFetcher project folder
   - Update `DataDumpFileName` in config if using a different date

4. **Run the application**
   - The app will automatically extract and parse the CSV from the ZIP

### Data Dump Format:
The ZIP contains a CSV file (`boardgames_ranks.csv`) with the following columns:
- `id`, `name`, `yearpublished`, `rank`, `bayesaverage`, `average`, `usersrated`
- Various rank categories (abstracts_rank, strategygames_rank, etc.)

The application reads: **id**, **name**, **yearpublished**, **rank**, and **bayesaverage**.

## Security Note

**DO NOT** store your Bearer Token in `BGGDataFetcher.json` if the file is committed to source control.
Instead, use .NET User Secrets:

```bash
dotnet user-secrets set "BggApi:BearerToken" "your-token-here"
```

The application will read from User Secrets first, overriding any value in the JSON file.

## Workflow Examples

### Get Quick Rankings (No API Details)
Perfect for quick analysis of current rankings:
```json
{
  "Settings": {
    "Count": 1000,
    "FetchGameDetails": false
  }
}
```
This completes in seconds!

### Build Complete Database
Get full details for top games:
```json
{
  "Settings": {
    "Count": 500,
    "FetchGameDetails": true
  }
}
```
Takes longer due to API calls, but provides comprehensive data.

### Update Existing Data
If you already have basic rankings, just fetch new details:
```json
{
  "Settings": {
    "LoadFromFile": true,
    "LoadFileName": "TopGames.json",
    "FetchGameDetails": true
  }
}
```

### Resume Interrupted Detail Fetching
If detail fetching was interrupted at game 900 out of 1500:
```json
{
  "Settings": {
  "LoadFromFile": true,
    "LoadFileName": "TopGames.json",
    "StartPosition": 900,
    "SaveDetailedFileName": "TopGamesDetailed.json",
    "FetchGameDetails": true
  }
}
```

**How it works:**
- Application automatically saves progress every 300 games
- Progress files: `TopGamesDetailed_progress_300.json`, `TopGamesDetailed_progress_600.json`, etc.
- Set `StartPosition` to last checkpoint to resume
- New run continues from that position
- Merge progress files for complete dataset

**Example Recovery:**
1. Run interrupted after processing 800 games
2. Check for `TopGamesDetailed_progress_600.json` (last checkpoint)
3. Set `"StartPosition": 600` in config
4. Re-run to continue from position 600
5. Merge `TopGamesDetailed_progress_600.json` with final output for complete data
