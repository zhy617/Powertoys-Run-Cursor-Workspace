# Pack x64 and ARM64 Release outputs into dist/ as CursorWorkspace-v{version}-win-{rid}.zip
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

if (Test-Path -LiteralPath $DistDir) {
    Remove-Item -LiteralPath $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

$x64Release = Join-Path $PluginRoot 'bin\x64\Release'
$arm64Release = Join-Path $PluginRoot 'bin\ARM64\Release'

$pairs = @(
    [pscustomobject]@{ ReleaseDir = $x64Release; Rid = 'win-x64' }
    [pscustomobject]@{ ReleaseDir = $arm64Release; Rid = 'win-arm64' }
)

foreach ($p in $pairs) {
    $releaseDir = $p.ReleaseDir
    if (-not (Test-Path -LiteralPath $releaseDir)) {
        throw "Release output not found (build Release first): $releaseDir"
    }
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
