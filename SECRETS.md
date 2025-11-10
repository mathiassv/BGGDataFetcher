# BGGDataFetcher - Secret Management

## Overview

This project uses .NET User Secrets to store sensitive configuration like API tokens. Secrets are stored **outside** the project directory and are **never** committed to source control.

## Setting Up Secrets

### 1. Initialize User Secrets (Already Done)
```bash
dotnet user-secrets init
```

### 2. Set Your Bearer Token
```bash
dotnet user-secrets set "BggApi:BearerToken" "your-token-here"
```

### 3. View Your Secrets (Optional)
```bash
dotnet user-secrets list
```

### 4. Remove a Secret (Optional)
```bash
dotnet user-secrets remove "BggApi:BearerToken"
```

## Where Are Secrets Stored?

Secrets are stored in a JSON file at:
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`
- **macOS/Linux**: `~/.microsoft/usersecrets/<user_secrets_id>/secrets.json`

The `<user_secrets_id>` is defined in `BGGDataFetcher.csproj` and is unique to this project.

## Alternative Secret Management Options

### 1. **Environment Variables** (Production)
```bash
# Windows (PowerShell)
$env:BggApi__BearerToken = "your-token-here"

# Linux/macOS
export BggApi__BearerToken="your-token-here"
```

Update `Program.cs` to add:
```csharp
.AddEnvironmentVariables()
```

### 2. **appsettings.json** (Not Recommended for Secrets)
Create `appsettings.json` (add to `.gitignore`!):
```json
{
  "BggApi": {
    "BearerToken": "your-token-here"
  }
}
```

Update `Program.cs` to add:
```csharp
.AddJsonFile("appsettings.json", optional: true)
```

### 3. **Azure Key Vault** (Enterprise/Cloud)
For production Azure deployments:
```bash
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
```

### 4. **.env File with dotenv** (Simple Alternative)
```bash
dotnet add package DotNetEnv
```

Create `.env` file (add to `.gitignore`!):
```
BGG_API_BEARER_TOKEN=your-token-here
```

## Best Practices

1. ? **Never** commit secrets to source control
2. ? Use **User Secrets** for local development
3. ? Use **Environment Variables** or **Key Vault** for production
4. ? Add `appsettings.json`, `.env`, or any file with secrets to `.gitignore`
5. ? Document which secrets are required in this README

## Required Secrets

| Key | Description | How to Obtain |
|-----|-------------|---------------|
| `BggApi:BearerToken` | BoardGameGeek API Bearer Token | Contact BGG or check their API documentation |

## Troubleshooting

### "Bearer token not found!" Error

If you see this error, make sure you've set the token:
```bash
dotnet user-secrets set "BggApi:BearerToken" "your-actual-token"
```

### Verify Secrets Are Set
```bash
dotnet user-secrets list
```

Should show:
```
BggApi:BearerToken = your-token-here
```
