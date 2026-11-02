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
    "DataDumpFileName": "Data/boardgames_ranks_*.zip",
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
    "DataDumpFileName": "Data/boardgames_ranks_*.zip",
    "Count": 500,
  "FetchGameDetails": true
  }
}
```

## Top 1000 Games, Skip Details (Fast)
```json
{
  "Settings": {
 "DataDumpFileName": "Data/boardgames_ranks_*.zip",
    "Count": 1000,
    "FetchGameDetails": false
  }
}
```

## Custom Output Files
```json
{
  "Settings": {
    "DataDumpFileName": "Data/boardgames_ranks_*.zip",
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
    "DataDumpFileName": "Data/boardgames_ranks_*.zip",
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
| `OutputFolder` | string | `"output"` | Folder for the working files: `TopGames.json`, `TopGamesDetailed.json` and the resumable `TopGamesDetailed.jsonl` (created automatically). A file name given as an absolute path is used as is. Per-game files go to `IndividualJsonOutputFolder` |
| `LoadFromFile` | bool | `false` | If `true`, load games from previously saved JSON file; if `false`, use data dump |
| `DataDumpFileName` | string | `"Data/boardgames_ranks_*.zip"` | BGG data dump ZIP path (used when LoadFromFile is false). With a wildcard the file with the **newest `yyyy-MM-dd` date in its name** is used, so a newly downloaded dump is picked up without changing the config. Use an exact file name to pin a specific dump |
| `Count` | int | `100` | Number of top games to fetch |
| `RankedOnly` | bool | `true` | If `true`, only ranked games are used and the list stops when the ranked games run out (the data dump has ~30,000 ranked base games, so a larger `Count` returns fewer). If `false`, unranked games (in data dump order) fill up the list until `Count` is reached. Expansions are always skipped |
| `LoadFileName` | string | `"TopGames.json"` | JSON file to load games from (only used when LoadFromFile is true) |
| `SaveBasicFileName` | string | `"TopGames.json"` | JSON file to save basic game data to |
| `SaveDetailedFileName` | string | `"TopGamesDetailed.json"` | JSON file to save detailed game data to |
| `FetchGameDetails` | bool | `true` | If `true`, fetch detailed game information from the API; if `false`, skip details |
| `RefreshAfterDays` | int | `0` | `0`: games already fetched are never fetched again. `N > 0`: games whose details are older than N days (see the `fetchedAt` field) are fetched again and replaced |
| `SaveIndividualJsonFiles` | bool | `false` | If `true`, save each game as a separate JSON file; if `false`, save only combined file |
| `IndividualJsonOutputFolder` | string | `"output/games"` | Folder path where individual game JSON files will be saved |

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
   - Save the ZIP file in the `Data` folder (keep the date in the file name)
   - The newest dump is picked automatically; only change `DataDumpFileName` to pin a specific one

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
The application appends every fetched batch to `TopGamesDetailed.jsonl` (in `OutputFolder`, next to `SaveDetailedFileName`). Resuming is automatic: just run it again and it skips every game already in that file, and retries any that failed (rate limiting, server errors). When all games are fetched it writes `TopGamesDetailed.json` (and the per-game files if enabled). Press Ctrl+C at any time to stop safely.

To fetch everything again from scratch, delete `TopGamesDetailed.jsonl`.

**Updating existing data:** every game has a `fetchedAt` timestamp (UTC). Set `RefreshAfterDays` to e.g. `30` and run again: only games that are new or older than 30 days are fetched, the rest is kept. If a refresh fails for a game, its old data is kept. Individual game files are named by BGG id (`224517.json`), so they are overwritten in place. Every run also applies the ranks of the current data dump to all games already fetched (no re-fetch needed) and writes the output in rank order. Exit codes: `0` done, `1` error, `2` done but some games could not be fetched, `130` interrupted with Ctrl+C. The `Data` folder is looked up from the working folder, then from the exe folder and its parents.

### Save Individual Game JSON Files
Save each game as a separate JSON file for easier processing:
```json
{
  "Settings": {
    "Count": 1000,
    "FetchGameDetails": true,
    "SaveIndividualJsonFiles": true,
    "IndividualJsonOutputFolder": "output/games"
  }
}
```

**How it works:**
- Each game is saved as `{gameId}.json` in the specified folder
- Example: `output/games/174430.json` for Gloomhaven
- Files contain complete detailed game data
- Combined JSON file is still saved as normal
- Useful for:
  - Processing games individually
  - Building game detail pages
  - Selective data loading
  - Distributed processing

**Benefits:**
- ? **Individual Access**: Load single games without parsing entire dataset
- ?? **Selective Updates**: Update specific games without re-fetching all
- ?? **Easy Integration**: Simple to integrate with other tools
- ?? **Version Control Friendly**: Easier to track changes to individual games
