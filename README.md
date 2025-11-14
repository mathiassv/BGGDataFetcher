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
| `SaveIndividualJsonFiles` | bool | `false` | If `true`, save each game as a separate JSON file; if `false`, save only combined file |
| `IndividualJsonOutputFolder` | string | `"output/games"` | Folder path where individual game JSON files will be saved |

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

**7. Save each game as a separate JSON file:**
If you want to save each game as an individual JSON file in addition to the combined file:
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
This will create files like `output/games/174430.json`, `output/games/161936.json`, etc., where the filename is the game ID.
