#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Switch between local osu project references and NuGet package references.

.DESCRIPTION
    This script modifies all .csproj files in the workspace to either use local
    project references from a sibling osu repository (../osu), or use NuGet
    packages with a specified version.

    When switching to local mode, the script adds XML comment markers to track
    which references were modified. When switching back to a NuGet version, the
    script uses these markers to correctly restore PackageReferences with their
    original metadata (e.g., PrivateAssets).

    Requires PowerShell 7+ (pwsh).

.PARAMETER Action
    'local' (default) to use local ../osu project references.
    A version string (e.g., '2025.1209.0') to use NuGet packages.

.EXAMPLE
    .\UseLocalOsu.ps1
    # Switch to local osu references

.EXAMPLE
    .\UseLocalOsu.ps1 local
    # Switch to local osu references (explicit)

.EXAMPLE
    .\UseLocalOsu.ps1 2025.1209.0
    # Switch to NuGet packages with version 2025.1209.0
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Action = "local"
)

$ErrorActionPreference = "Stop"

# Show informational messages by default; callers can pass -InformationAction to override
if (-not $PSBoundParameters.ContainsKey('InformationAction')) {
    $InformationPreference = 'Continue'
}

$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) {
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$OsuBasePath = [System.IO.Path]::GetFullPath((Join-Path $ScriptDir "../osu"))

function Get-OsuProjectRefPath {
    param(
        [string]$PackageName,
        [string]$CsprojDirectory
    )

    $projectName = $PackageName -replace '^ppy\.', ''
    $targetFullPath = [System.IO.Path]::GetFullPath(
        [System.IO.Path]::Combine($OsuBasePath, $projectName, "$projectName.csproj")
    )

    $relativePath = [System.IO.Path]::GetRelativePath($CsprojDirectory, $targetFullPath)
    return $relativePath -replace '\\', '/'
}

# --- Validate inputs ---

if ($Action -eq "-h" -or $Action -eq "--help") {
    Get-Help $MyInvocation.MyCommand.Path -Detailed
    exit 0
}

if ($Action -eq "local") {
    if (-not (Test-Path $OsuBasePath -PathType Container)) {
        Write-Error "osu repository not found at: $OsuBasePath. Expected the osu repository to be cloned as a sibling directory (../osu)."
    }
    Write-Information "Switching to local osu references from: $OsuBasePath"
}
else {
    Write-Information "Switching to osu NuGet version: $Action"
}

Write-Information ""

# --- Process all csproj files ---

$csprojFiles = Get-ChildItem -Path $ScriptDir -Filter "*.csproj" -Recurse -File
$updatedCount = 0

foreach ($file in $csprojFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    $original = $content
    $csprojDir = $file.DirectoryName

    # Detect line ending style and use the same for replacements
    $nl = if ($content -match "`r`n") { "`r`n" } else { "`n" }

    if ($Action -eq "local") {
        # ==========================================================
        # LOCAL MODE: PackageReference -> ProjectReference
        # ==========================================================

        # Pattern 1: Multi-line <PackageReference> with <PrivateAssets>all</PrivateAssets>
        # e.g.:
        #   <PackageReference Include="ppy.osu.Game" Version="2025.1209.0">
        #     <PrivateAssets>all</PrivateAssets>
        #   </PackageReference>
        $content = [regex]::Replace($content,
            '(?m)^([ \t]*)<PackageReference Include="(ppy\.osu\.[^"]*)" Version="[^"]*">[ \t]*\r?\n[ \t]*<PrivateAssets>all</PrivateAssets>[ \t]*\r?\n[ \t]*</PackageReference>',
            {
                param($m)
                $indent = $m.Groups[1].Value
                $pkg = $m.Groups[2].Value
                $ref = Get-OsuProjectRefPath -PackageName $pkg -CsprojDirectory $csprojDir
                "${indent}<!-- UseLocalOsu: ${pkg} PrivateAssets=all -->${nl}${indent}<ProjectReference Include=`"${ref}`">${nl}${indent}  <Private>false</Private>${nl}${indent}</ProjectReference>"
            })

        # Pattern 2: Self-closing <PackageReference ... />
        # e.g.:
        #   <PackageReference Include="ppy.osu.Game" Version="2025.1209.0" />
        $content = [regex]::Replace($content,
            '(?m)^([ \t]*)<PackageReference Include="(ppy\.osu\.[^"]*)" Version="[^"]*"\s*/>',
            {
                param($m)
                $indent = $m.Groups[1].Value
                $pkg = $m.Groups[2].Value
                $ref = Get-OsuProjectRefPath -PackageName $pkg -CsprojDirectory $csprojDir
                "${indent}<!-- UseLocalOsu: ${pkg} -->${nl}${indent}<ProjectReference Include=`"${ref}`" />"
            })
    }
    else {
        # ==========================================================
        # VERSION MODE: Restore PackageReferences / update version
        # ==========================================================
        $version = $Action

        # Reverse Pattern 1: Restore multi-line ProjectReference (was PrivateAssets=all)
        $content = [regex]::Replace($content,
            '(?m)^([ \t]*)<!-- UseLocalOsu: (ppy\.osu\.\S+) PrivateAssets=all -->[ \t]*\r?\n[ \t]*<ProjectReference Include="[^"]*">[ \t]*\r?\n[ \t]*<Private>false</Private>[ \t]*\r?\n[ \t]*</ProjectReference>',
            {
                param($m)
                $indent = $m.Groups[1].Value
                $pkg = $m.Groups[2].Value
                "${indent}<PackageReference Include=`"${pkg}`" Version=`"${version}`">${nl}${indent}  <PrivateAssets>all</PrivateAssets>${nl}${indent}</PackageReference>"
            })

        # Reverse Pattern 2: Restore self-closing ProjectReference
        $content = [regex]::Replace($content,
            '(?m)^([ \t]*)<!-- UseLocalOsu: (ppy\.osu\.\S+) -->[ \t]*\r?\n[ \t]*<ProjectReference Include="[^"]*"\s*/>',
            {
                param($m)
                $indent = $m.Groups[1].Value
                $pkg = $m.Groups[2].Value
                "${indent}<PackageReference Include=`"${pkg}`" Version=`"${version}`" />"
            })

        # Update version on any remaining PackageReferences (already NuGet, just version change)
        $content = $content -replace '(<PackageReference Include="ppy\.osu\.[^"]*" Version=")[^"]*(")', "`${1}${version}`$2"
    }

    if ($content -ne $original) {
        # Detect if original file had a UTF-8 BOM and preserve it
        $originalBytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $hasBom = $originalBytes.Length -ge 3 -and $originalBytes[0] -eq 0xEF -and $originalBytes[1] -eq 0xBB -and $originalBytes[2] -eq 0xBF
        $encoding = if ($hasBom) { [System.Text.UTF8Encoding]::new($true) } else { [System.Text.UTF8Encoding]::new($false) }
        [System.IO.File]::WriteAllText($file.FullName, $content, $encoding)
        $updatedCount++
        $relPath = [System.IO.Path]::GetRelativePath($ScriptDir, $file.FullName) -replace '\\', '/'
        Write-Information "  Updated: $relPath"
    }
}

Write-Information ""
if ($updatedCount -eq 0) {
    Write-Information "No files were modified."
}
elseif ($Action -eq "local") {
    Write-Information "Switched $updatedCount file(s) to local osu project references."
    Write-Information "To restore NuGet references: .\UseLocalOsu.ps1 <version>"
}
else {
    Write-Information "Updated $updatedCount file(s) to osu NuGet version: $Action"
}
