#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Publishes project(s) for release while avoiding local osu project-reference asset leakage.

.DESCRIPTION
    If the repository is currently in "local osu references" mode (detected by
    "<!-- UseLocalOsu: ... -->" markers), this script temporarily switches to
    NuGet osu references, runs dotnet publish, and then restores local references.

    Before publishing, this script packs required local osu projects from ../osu
    into a local NuGet feed, and publishes plugin targets using those local packages.

.PARAMETER OsuVersion
    NuGet version of ppy.osu.* packages used for local packing/publishing.
    Required.
    Can be passed positionally as the first argument.

.PARAMETER Target
    Publish target path. Defaults to the whole solution.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER Output
    Output directory for dotnet publish.
    Defaults to artifacts/publish.
    Ignored for solution targets (.sln/.slnx) because dotnet doesn't support -o there.

.PARAMETER NoRestore
    Pass --no-restore to dotnet publish.

.PARAMETER NoBuild
    Pass --no-build to dotnet publish.

.PARAMETER DotnetPublishArgs
    Extra arguments forwarded to dotnet publish.

.EXAMPLE
    .\PackForRelease.ps1 2025.1209.0

.EXAMPLE
    .\PackForRelease.ps1 2025.1209.0 -Target osu.Game.Plugins\osu.Game.Plugins.csproj

.EXAMPLE
    .\PackForRelease.ps1 -Target osu-plugins.slnx -Configuration Release -- -p:ContinuousIntegrationBuild=true
#>

param(
    [Parameter(Position = 0)]
    [string]$OsuVersion,
    [string]$Target = "osu-plugins.slnx",
    [string]$Configuration = "Release",
    [string]$Output = "artifacts/publish",
    [switch]$NoRestore,
    [switch]$NoBuild,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DotnetPublishArgs
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
if (-not $scriptDir) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$useLocalScript = Join-Path $scriptDir "UseLocalOsu.ps1"
if (-not (Test-Path $useLocalScript -PathType Leaf)) {
    Write-Error "Missing script: $useLocalScript"
}

if ([string]::IsNullOrWhiteSpace($OsuVersion)) {
    Write-Error "Please pass a version number (e.g. .\PackForRelease.ps1 2025.1209.0)."
}

$osuRepoPath = [System.IO.Path]::GetFullPath((Join-Path $scriptDir "../osu"))
if (-not (Test-Path $osuRepoPath -PathType Container)) {
    Write-Error "osu repository not found at: $osuRepoPath"
}

Push-Location $scriptDir
$switchedFromLocal = $false
$capturedError = $null
$tempNuGetConfig = $null
try {
    $localModeActive = Get-ChildItem -Path . -Filter "*.csproj" -Recurse -File |
        Select-String -Pattern "<!-- UseLocalOsu:" -SimpleMatch -Quiet

    $packageIdsFromNuGet = Get-ChildItem -Path . -Filter "*.csproj" -Recurse -File |
        Select-String -Pattern '<PackageReference Include="(ppy\.osu\.[^"]+)"' -AllMatches |
        ForEach-Object { $_.Matches } |
        ForEach-Object { $_.Groups[1].Value }

    $packageIdsFromMarkers = Get-ChildItem -Path . -Filter "*.csproj" -Recurse -File |
        Select-String -Pattern '<!-- UseLocalOsu: (ppy\.osu\.[^\s]+)' -AllMatches |
        ForEach-Object { $_.Matches } |
        ForEach-Object { $_.Groups[1].Value }

    $packageIds = @($packageIdsFromNuGet + $packageIdsFromMarkers | Sort-Object -Unique)

    if (-not $packageIds -or $packageIds.Count -eq 0) {
        throw "No ppy.osu.* PackageReference entries were found in this repository."
    }

    $localFeedPath = Join-Path $scriptDir "artifacts/local-osu-feed"
    # Clean previous local feed to avoid stale packages
    if (Test-Path $localFeedPath) {
        Remove-Item -Recurse -Force $localFeedPath
    }
    New-Item -Path $localFeedPath -ItemType Directory -Force | Out-Null

    $localPackagesPath = Join-Path $scriptDir "artifacts/local-osu-packages"
    if (Test-Path $localPackagesPath) {
        Remove-Item -Recurse -Force $localPackagesPath
    }
    New-Item -Path $localPackagesPath -ItemType Directory -Force | Out-Null

    Write-Host "Packing local osu packages from: $osuRepoPath"
    foreach ($packageId in $packageIds) {
        $projectName = $packageId -replace '^ppy\.', ''
        $projectPath = Join-Path $osuRepoPath "$projectName/$projectName.csproj"
        if (-not (Test-Path $projectPath -PathType Leaf)) {
            throw "Local osu project not found for package $packageId at: $projectPath"
        }

        Write-Host "  Packing $packageId"
        & dotnet pack $projectPath -c $Configuration -o $localFeedPath -p:Version=$OsuVersion
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to pack local osu project: $projectPath"
        }
    }

    $tempNuGetConfig = Join-Path ([System.IO.Path]::GetTempPath()) ("nuget.local-osu." + [System.Guid]::NewGuid().ToString("N") + ".config")
    $normalizedFeedPath = [System.IO.Path]::GetFullPath($localFeedPath)
    $nugetConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-osu" value="$normalizedFeedPath" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@
    [System.IO.File]::WriteAllText($tempNuGetConfig, $nugetConfigContent, [System.Text.UTF8Encoding]::new($false))

    if ($localModeActive) {
        Write-Host "Detected local osu references. Switching to NuGet version $OsuVersion for publishing..."
        & $useLocalScript $OsuVersion
        if (-not $?) {
            throw "Failed to switch to NuGet references."
        }
        $switchedFromLocal = $true
    }
    elseif (-not [string]::IsNullOrWhiteSpace($OsuVersion)) {
        Write-Host "Applying requested NuGet version $OsuVersion before publishing..."
        & $useLocalScript $OsuVersion
        if (-not $?) {
            throw "Failed to apply NuGet version."
        }
    }

    $isSolutionTarget = $Target -match '\.slnx?$'
    # Use isolated packages path to bypass NuGet global cache (which may have stale nuget.org packages)
    $publishArguments = @("publish", $Target, "-c", $Configuration, "--configfile", $tempNuGetConfig, "-p:RestorePackagesPath=$localPackagesPath")

    if (-not [string]::IsNullOrWhiteSpace($Output)) {
        if ($isSolutionTarget) {
            Write-Host "Output path is ignored for solution target: $Target"
        }
        else {
            $publishArguments += @("-o", $Output)
        }
    }

    if ($NoRestore) { $publishArguments += "--no-restore" }
    if ($NoBuild) { $publishArguments += "--no-build" }
    if ($DotnetPublishArgs) { $publishArguments += $DotnetPublishArgs }

    Write-Host ""
    Write-Host "Running: dotnet $($publishArguments -join ' ')"
    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    Write-Host ""
    Write-Host "Publish completed successfully."
}
catch {
    $capturedError = $_
}
finally {
    if ($tempNuGetConfig -and (Test-Path $tempNuGetConfig -PathType Leaf)) {
        Remove-Item -Force $tempNuGetConfig -ErrorAction SilentlyContinue
    }

    if ($switchedFromLocal) {
        try {
            Write-Host "Restoring local osu references..."
            & $useLocalScript local
            if (-not $?) {
                throw "Failed to restore local references."
            }
            Write-Host "Local references restored."
        }
        catch {
            if (-not $capturedError) {
                $capturedError = $_
            }
            else {
                Write-Error "Additional error while restoring local references: $_"
            }
        }
    }

    Pop-Location
}

if ($capturedError) {
    Write-Error $capturedError
    exit 1
}
