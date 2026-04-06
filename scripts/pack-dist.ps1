# Pack x64 and ARM64 Release outputs into dist/ as CursorWorkspace-v{version}-win-{rid}.zip
# Syncs Version in src/.../plugin.json to match -Version, then copies it into Release outputs before zipping.
# Usage: .\scripts\pack-dist.ps1 -Version 1.0.0
#        .\scripts\pack-dist.ps1 -Version v2.3.4 -Build
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $Version,

    [switch] $Build
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$PluginRoot = Join-Path $RepoRoot 'src\Community.PowerToys.Run.Plugin.CursorWorkspaces'
$DistDir = Join-Path $RepoRoot 'dist'

$Version = $Version.Trim().TrimStart('v')
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw 'Version must not be empty.'
}

$pluginJsonPath = Join-Path $PluginRoot 'plugin.json'
if (-not (Test-Path -LiteralPath $pluginJsonPath)) {
    throw "plugin.json not found: $pluginJsonPath"
}
$pluginJsonText = [System.IO.File]::ReadAllText($pluginJsonPath)
if ($pluginJsonText -notmatch '"Version"\s*:\s*"[^"]*"') {
    throw "Could not find `"Version`" in plugin.json: $pluginJsonPath"
}
$pluginJsonText = [regex]::Replace($pluginJsonText, '("Version"\s*:\s*")[^"]*(")', "`${1}$Version`${2}", 1)
[System.IO.File]::WriteAllText($pluginJsonPath, $pluginJsonText, [System.Text.UTF8Encoding]::new($false))

if ($Build) {
    Push-Location $PluginRoot
    try {
        dotnet build -c Release -p:Platform=x64
        dotnet build -c Release -p:Platform=ARM64
    }
    finally {
        Pop-Location
    }
}

$x64Release = Join-Path $PluginRoot 'bin\x64\Release'
$arm64Release = Join-Path $PluginRoot 'bin\ARM64\Release'

if (Test-Path -LiteralPath $DistDir) {
    Remove-Item -LiteralPath $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

$pairs = @(
    [pscustomobject]@{ ReleaseDir = $x64Release; Rid = 'win-x64' }
    [pscustomobject]@{ ReleaseDir = $arm64Release; Rid = 'win-arm64' }
)

foreach ($p in $pairs) {
    $releaseDir = $p.ReleaseDir
    if (-not (Test-Path -LiteralPath $releaseDir)) {
        throw "Release output not found (build Release first): $releaseDir"
    }
    Copy-Item -LiteralPath $pluginJsonPath -Destination (Join-Path $releaseDir 'plugin.json') -Force
    $items = @(Get-ChildItem -LiteralPath $releaseDir -Force)
    if ($items.Count -eq 0) {
        throw "Release folder is empty: $releaseDir"
    }

    $zipName = "CursorWorkspace-v$Version-$($p.Rid).zip"
    $zipPath = Join-Path $DistDir $zipName
    $glob = Join-Path $releaseDir '*'
    Compress-Archive -Path $glob -DestinationPath $zipPath -Force
    Write-Host "Created: $zipPath"
}

Write-Host "Done. Output: $DistDir"
