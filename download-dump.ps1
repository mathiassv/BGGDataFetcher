<#
.SYNOPSIS
  Downloads a BoardGameGeek ranks data dump into the Data folder.

.DESCRIPTION
  BGG only shows the download link on https://boardgamegeek.com/data_dumps/bg_ranks when you are logged in,
  and the link is a pre-signed URL that is valid for 10 minutes. The bearer token used for the XML API does
  not work for that page, so this script takes the link from you:

    1. Log in at https://boardgamegeek.com/data_dumps/bg_ranks
    2. Right-click "Click to Download" -> "Copy link address"
    3. Run:  .\download-dump.ps1 -Url '<paste the link>'

  Put the link in SINGLE quotes - it contains & characters. The file is saved under the name BGG gave it
  (boardgames_ranks_yyyy-MM-dd.zip), and the app automatically uses the newest one in the Data folder.

.PARAMETER Url
  The signed download link copied from the BGG page.

.PARAMETER OutputDir
  Where to save the zip, relative to this script or absolute (default: Data).

.PARAMETER Force
  Overwrite the file if it already exists.

.EXAMPLE
  .\download-dump.ps1 -Url 'https://geek-export-stats.s3.amazonaws.com/boardgames_export/boardgames_ranks_2026-11-01.zip?X-Amz-...'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Url,
    [string]$OutputDir = 'Data',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# Never print the full URL: the query string contains the signature
$uri = $null
if (-not [Uri]::TryCreate($Url.Trim(), [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @('https', 'http')) {
    throw "That is not a valid http(s) link. Copy the link address of 'Click to Download' and put it in single quotes."
}

$fileName = [System.IO.Path]::GetFileName($uri.AbsolutePath)
if ($fileName -notmatch '^boardgames_ranks_\d{4}-\d{2}-\d{2}\.zip$') {
    throw "Unexpected file name '$fileName'. Expected boardgames_ranks_yyyy-MM-dd.zip - is this the ranks data dump link?"
}

$outDir = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, $OutputDir))
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$target = Join-Path $outDir $fileName

if ((Test-Path -LiteralPath $target) -and -not $Force) {
    Write-Host "$fileName already exists in $outDir - nothing to do (use -Force to download again)." -ForegroundColor Yellow
    return
}

$temp = "$target.download"
try {
    Write-Host "Downloading $fileName from $($uri.Host) ..." -ForegroundColor Cyan
    try {
        Invoke-WebRequest -Uri $uri -OutFile $temp -UseBasicParsing
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        if ($status -in 400, 403) {
            throw "The server refused the link (HTTP $status). The link is only valid for 10 minutes - reload the BGG page, copy a fresh link and run the script again."
        }
        throw
    }

    # Make sure we got a real data dump and not an error page
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($temp)
    }
    catch {
        throw "The downloaded file is not a valid zip (the link may have expired or returned an error page)."
    }
    try {
        if (-not ($zip.Entries | Where-Object { $_.FullName -eq 'boardgames_ranks.csv' })) {
            throw "The downloaded file does not contain boardgames_ranks.csv - not a valid ranks data dump."
        }
    }
    finally {
        $zip.Dispose()
    }

    Move-Item -LiteralPath $temp -Destination $target -Force
    $sizeMb = (Get-Item -LiteralPath $target).Length / 1MB
    Write-Host ("Saved {0} ({1:N1} MB) to {2}" -f $fileName, $sizeMb, $outDir) -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force }
}
