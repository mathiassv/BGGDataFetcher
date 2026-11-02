<#
.SYNOPSIS
  Builds BGGDataFetcher in Release configuration.

.DESCRIPTION
  Restores packages and builds the solution in Release mode. With -Publish the app is also published
  to a folder (default: .\publish) so it can be copied and run elsewhere.

.PARAMETER Publish
  Also run "dotnet publish" for a ready-to-run output folder.

.PARAMETER OutputDir
  Publish folder, relative to this script or absolute (default: publish). Only used with -Publish.

.PARAMETER Clean
  Delete previous Release build output before building.

.EXAMPLE
  .\build-release.ps1

.EXAMPLE
  .\build-release.ps1 -Publish -Clean
#>
[CmdletBinding()]
param(
    [switch]$Publish,
    [string]$OutputDir = 'publish',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

$solution = Join-Path $PSScriptRoot 'BGGDataFetcher.sln'
$project  = Join-Path $PSScriptRoot 'BGGDataFetcher.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK was not found. Install it from https://dotnet.microsoft.com/download'
}

function Invoke-DotNet {
    param([string[]]$Arguments)
    Write-Host "> dotnet $($Arguments -join ' ')" -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE"
    }
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

if ($Clean) {
    Invoke-DotNet @('clean', $solution, '-c', 'Release', '--nologo', '-v', 'minimal')
}

Invoke-DotNet @('restore', $solution, '--nologo')
Invoke-DotNet @('build', $solution, '-c', 'Release', '--no-restore', '--nologo', '-warnaserror')

$releaseDir = Join-Path $PSScriptRoot 'bin\Release'
Write-Host "Release build output: $releaseDir" -ForegroundColor Green

if ($Publish) {
    # Path.Combine keeps an absolute -OutputDir as is and resolves a relative one against the script folder
    $publishDir = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, $OutputDir))
    Invoke-DotNet @('publish', $project, '-c', 'Release', '--no-build', '--nologo', '-o', $publishDir)
    Write-Host "Published to: $publishDir" -ForegroundColor Green
    Write-Host 'Note: BGGDataFetcher.json (your local config) is copied into the output; do not share the folder if it contains a bearer token.' -ForegroundColor Yellow
}

$stopwatch.Stop()
Write-Host ("Done in {0:N1}s" -f $stopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
