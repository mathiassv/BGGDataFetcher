# BoardGameGeek Data Fetcher

A .NET 9 console application that collects detailed information about top-ranked board games from [BoardGameGeek](https://boardgamegeek.com) using BGG's official ranked games data dumps and API.

## ✨ Features

- **BGG Ranked Games Data Dump Import**: Fast import from official BGG ranked games data dumps (required)
- **API Integration**: Enriches game data using BoardGameGeek's XML API2
- **Comprehensive Data**: Collects extensive game information including:
  - Basic info (name, year, rank, ratings)
  - Player count recommendations with voting data
  - Game mechanics and categories
  - Detailed statistics (owners, wishlist, complexity)
  - Playing time and age recommendations
- **JSON Export**: Saves data in JSON format
- **Configuration-Based**: Control behavior via `BGGDataFetcher.json` config file
- **Flexible Workflow**: Use data dump or load from existing files
- **Secure**: Uses .NET User Secrets for API token management
- **Dual Logging**: Clean console output + detailed file logging
- **Error Recovery**: Automatic batch splitting and retry logic
- **Progress Tracking**: Auto-save every 300 games with resume capability

## 🚀 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- BoardGameGeek API Bearer Token (see instructions below)
- **BGG Data Dump file** (see instructions below)

### 🔑 Getting Your BGG API Bearer Token

To use the BoardGameGeek API, you need to obtain a Bearer Token by registering your application with BGG.

### Step-by-Step Instructions:

1. **Log In to BoardGameGeek**
   - Go to https://boardgamegeek.com and log in with your account

2. **Register Your Application**
   - Visit https://boardgamegeek.com/applications (you must be logged in)
   - Click on "Register Application" or similar option
   - Fill in the application details:
     - **Application Name**: e.g., "BGG Data Fetcher"
     - **Description**: Brief description of what your application does
     - **Redirect URL**: Can use a placeholder like `http://localhost` if not needed
   - Submit your application for approval

3. **Wait for Approval**
   - BGG staff will review your application
   - You'll receive a notification when approved (this may take some time)

4. **Create a Token**
   - Once approved, go back to https://boardgamegeek.com/applications
   - Find your application in the list
   - Click on it to view details
   - Look for an option to "Create New Token" or "Generate Token"
   - Copy the generated Bearer Token (you'll need this for the next step)

5. **Configure the Token**
   - Use the token with the setup command in the Installation section below
   - **Keep your token secure** - don't share it or commit it to source control

### Installation

1. **Clone the repository**
   ```bash
git clone <repository-url>
   cd BGGDataFetcher
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Set up configuration**
   ```bash
   cp BGGDataFetcher.json.template BGGDataFetcher.json
   ```

4. **Set up your API token (recommended method)**
   ```bash
   dotnet user-secrets set "BggApi:BearerToken" "your-token-here"
   ```
   
   See [SECRETS.md](SECRETS.md) for more details on secret management.

5. **Download BGG Data Dump** (see instructions below)

## 📦 Getting the BGG Ranked Games Data Dump (Required)

The application requires BGG's official ranked games data dump file to get game rankings. This data dump contains minimal information (ID, name, year, rank, ratings) but includes all games in ranked order as a CSV file. **You must be logged in to BoardGameGeek to download this file.**

### Step-by-Step Instructions:

1. **Create a BGG Account** (if you don't have one)
   - Go to https://boardgamegeek.com
   - Click "Join" or "Sign Up"
   - Create your free account

2. **Log In to BoardGameGeek**
   - Go to https://boardgamegeek.com
   - Click "Login"
   - Enter your credentials

3. **Download the Data Dump**
   - While logged in, visit: https://boardgamegeek.com/data_dumps/bg_ranks
   - You'll see a list of available data dumps with dates
   - Click the download link for the latest file (e.g., `boardgames_ranks_2025-11-07.zip`)
   - **Important**: You must be logged in or the page will show "Access Denied"

4. **Place the File in Your Project**
   - Save the downloaded ZIP file to your project directory (same folder as `BGGDataFetcher.csproj`)
   - The default expected filename is `boardgames_ranks_2025-11-07.zip`
   - You can use a different filename by updating `DataDumpFileName` in `BGGDataFetcher.json`

### Data Dump Contents

The ZIP file contains a CSV file (`boardgames_ranks.csv`) with columns:
- `id` - Game ID
- `name` - Game name
- `yearpublished` - Year published
- `rank` - Overall BGG rank
- `bayesaverage` - Geek rating (Bayes average)
- `average` - Average user rating
- `usersrated` - Number of user ratings
- Plus various category-specific ranks

### Troubleshooting

**"Access Denied" or "404 Not Found"**
- Make sure you're logged in to BoardGameGeek
- Try visiting https://boardgamegeek.com first, then the data dumps page
- Clear your browser cache and try again

**"Data dump file not found"**
- Check that the ZIP file is in the same directory as your project
- Verify the filename matches `DataDumpFileName` in `BGGDataFetcher.json`
- Make sure the file downloaded completely (should be several MB)

## ⚙️ Configuration

Edit `BGGDataFetcher.json` to control the application's behavior:

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
"StartPosition": 0,
    "LoadFileName": "TopGames.json",
    "SaveBasicFileName": "TopGames.json",
    "SaveDetailedFileName": "TopGamesDetailed.json",
    "FetchGameDetails": true
  }
}
```

### Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LoadFromFile` | bool | `false` | If `true`, load from previously saved JSON file; if `false`, use data dump |
| `DataDumpFileName` | string | `"boardgames_ranks_2025-11-07.zip"` | BGG data dump ZIP file path (used when LoadFromFile is false) |
| `Count` | int | `100` | Number of top games to fetch |
| `StartPosition` | int | `0` | Start position for fetching details (0-based index, for resuming interrupted runs) |
| `LoadFileName` | string | `"TopGames.json"` | File to load games from (when LoadFromFile is true) |
| `SaveBasicFileName` | string | `"TopGames.json"` | File to save basic game data to |
| `SaveDetailedFileName` | string | `"TopGamesDetailed.json"` | File to save detailed game data to |
| `FetchGameDetails` | bool | `true` | If `true`, fetch detailed API information; if `false`, skip details |

### Logging Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LogLevel` | string | `"Information"` | Logging verbosity: `"Trace"`, `"Debug"`, `"Information"`, `"Warning"`, `"Error"`, `"Critical"`, or `"None"` |

See [BGGDataFetcher.Configuration.md](BGGDataFetcher.Configuration.md) for more examples.

#### Common Workflows

**1. Get top 100 games with full details (Default):**
```bash
dotnet run
```

**2. Get top 1000 games with full details:**
Edit `BGGDataFetcher.json`:
```json
{
  "Settings": {
    "Count": 1000,
    "FetchGameDetails": true
  }
}
```

**3. Get top 5000 games, basic info only (fast):**
```json
{
  "Settings": {
    "Count": 5000,
    "FetchGameDetails": false
  }
}
```

**4. Load previously saved games and fetch details:**
```json
{
  "Settings": {
    "LoadFromFile": true,
    "LoadFileName": "TopGames.json",
    "FetchGameDetails": true
  }
}
```

**5. Use a different data dump file:**
```json
{
  "Settings": {
    "DataDumpFileName": "boardgames_ranks_2025-12-01.zip",
    "Count": 500
  }
}
```

**6. Resume interrupted detail fetching:**
If your detail fetching was interrupted (e.g., stopped at game 600 out of 1000):
```json
{
  "Settings": {
    "LoadFromFile": true,
    "LoadFileName": "TopGames.json",
  "StartPosition": 600,
    "FetchGameDetails": true
  }
}
```
The application automatically saves progress every 300 games to files like `TopGamesDetailed_progress_300.json`, `TopGamesDetailed_progress_600.json`, etc.

### Usage

**Run the application:**
```bash
dotnet run
```

The application will:
1. Read game rankings from the data dump
2. Save basic game data to JSON
3. Fetch detailed information from BGG API (if enabled)
4. Save detailed data to JSON
5. Display a summary of the top 10 games
6. Show total execution time

## 📁 Project Structure

```
BGGDataFetcher/
├── Configuration/
│   ├── BggApiSettings.cs      # API configuration model
│   ├── LoggingSettings.cs     # Logging configuration model
│   └── BGGDataFetcherSettings.cs     # Settings configuration model
├── Interfaces/
│   └── IConsoleOutput.cs      # Console output abstraction interface
├── Models/
│   ├── BoardGame.cs           # Main game data models
│   ├── BoardGameBasic.cs      # Basic game information
│   └── PlayerCountRecommendation.cs # Player count voting data
├── Services/
│   ├── BGGDataFetcher.cs      # Main orchestrator
│   ├── DataDumpReader.cs      # Data dump ZIP/CSV reader
│   ├── BggApiClient.cs        # BGG API communication
│   ├── XmlProcessor.cs        # XML parsing helper
│   ├── GameEnricher.cs        # XML parsing and data enrichment
│   ├── FileManager.cs  # File I/O operations
│   └── ConsoleOutput.cs       # Console output implementation
├── Program.cs        # Application entry point
├── BGGDataFetcher.json    # Configuration file (gitignored)
├── BGGDataFetcher.json.template   # Configuration template
├── BGGDataFetcher.Configuration.md # Configuration examples
├── SECRETS.md       # Secret management guide
└── README.md     # This file
```

## 📊 Output Files

The application generates multiple files:

1. **TopGames.json** - Basic game data from data dump
2. **TopGamesDetailed.json** - Full enriched data with all details from API
3. **TopGamesDetailed_progress_XXX.json** - Automatic progress checkpoints (every 300 games)
4. **logs/bgg_datafetcher_{Date}.log** - Daily structured application logs
5. **bgg_errors.log** - Error log file (created only if errors occur)

### Error Logging

The application automatically logs all errors encountered during processing:

**Error Log File (`bgg_errors.log`):**
- Contains timestamped error messages
- Logs HTTP errors, XML parsing failures, and enrichment errors
- Includes game IDs and batch information for context
- Created automatically when errors occur

**XML Parsing Retry Logic:**
- Automatically splits batch in half if XML parsing fails
- Recursively fetches smaller batches until success or single-game level
- At single-game level, logs error and skips problematic game
- Prevents entire run from failing due to malformed XML in batch
- Combines results from successful sub-batches

**Example Batch Splitting Flow:**
```
Batch of 20 games fails XML parsing
  ├─> Split into 2 batches of 10 games
  │    ├─> First 10: Success ✓
  │    └─> Second 10: Fails, split again
  │       ├─> 5 games: Success ✓
  │         └─> 5 games: Fails, split again
  │          ├─> 2 games: Success ✓
  │      └─> 3 games: Split to individual
  │     ├─> Game 1: Success ✓
  │    ├─> Game 2: Fails, skip ✗
  │           └─> Game 3: Success ✓
Result: 18 out of 20 games fetched successfully
```

The application will notify you at the end if any errors occurred and where to find the error log.

## 📦 NuGet Packages

- **Microsoft.Extensions.Configuration** - Configuration management
- **Microsoft.Extensions.Configuration.Json** - JSON configuration provider
- **Microsoft.Extensions.Configuration.UserSecrets** - Secure secret storage
- **Microsoft.Extensions.Configuration.Binder** - Configuration binding
- **Microsoft.Extensions.Logging** - Logging abstractions
- **Microsoft.Extensions.DependencyInjection** - Dependency injection container
- **Serilog.Extensions.Logging.File** - File logging provider
- **System.Net.Http.Json** - HTTP client utilities

## 🏗️ Architecture

### Console Output Abstraction

The application uses an `IConsoleOutput` interface (located in `BGGDataFetcher.Interfaces` namespace) to abstract console output, making it testable and flexible:

```csharp
namespace BGGDataFetcher.Interfaces;

public interface IConsoleOutput
{
  void WriteInfo(string message);
  void WriteInfo(string message, params object[] args);
  void WriteError(string message);
  void WriteWarning(string message);
  void WriteDebug(string message);
  void WriteLine(string message);
  void WriteLine();
}
```

The default implementation (`ConsoleOutput` in `BGGDataFetcher.Services` namespace) writes directly to `Console` with color coding:
- **Info**: Default color
- **Error**: Red
- **Warning**: Yellow
- **Debug**: Gray

**Benefits:**
- **Testable**: Easy to mock for unit tests
- **Flexible**: Can switch output implementations (console, file, etc.)
- **Clean**: Simple API for output operations
- **Colored output**: Visual distinction between message types
- **No dependencies**: Direct console output, no logging framework needed

### Logging Architecture

```
Program.cs
  └─> IConsoleOutput (BGGDataFetcher.Interfaces)
       └─> ConsoleOutput (BGGDataFetcher.Services)
            └─> Console.WriteLine (direct output)

Services (BGGDataFetcher, BggApiClient, DataDumpReader, FileManager)
  └─> ILogger<T> (Microsoft.Extensions.Logging)
       └─> File (logs/bgg_fetcher_{Date}.log)

BggApiClient (Dual Logging)
  ├─> IConsoleOutput → Console output (user-friendly progress)
  └─> ILogger<BggApiClient> → File logging (detailed diagnostics)
```

**Program.cs** uses `IConsoleOutput` for simple, clean console output with colors.

**Services** use `ILogger<T>` for structured logging to files.

**BggApiClient** uses **both**:
- `IConsoleOutput` for real-time progress updates to console
- `ILogger` for detailed diagnostic logging to file

This dual approach provides:
- Simple, readable output for end users (console via IConsoleOutput)
- Detailed, structured logging for debugging and diagnostics (file via ILogger)
- Real-time progress visibility while keeping a complete log history

## 📌 Version History

### v1.0.0
- BGG data dump import (ZIP/CSV parsing)
- BGG API integration for detailed game information
- JSON export
- User Secrets integration for secure token storage
- Configuration-based approach via `BGGDataFetcher.json`
- Flexible workflows: data dump or load from file
- Automatic error logging and XML error capture
- Progress saving with automatic checkpoints every 300 games
- Resume capability from any position
- Dual logging system (console + file)
- Configurable log levels
- Automatic batch splitting for XML parsing failures
- Rate limiting handling with automatic retry
- Execution time tracking

---

Made with ❤️ for the board game community

## 📄 License & Copyright

Copyright © 2025 Mathias Svensson. All rights reserved.

Developed by Mathias Svensson (GitHub Copilot helped).
