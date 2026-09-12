[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $SptRoot,
    [string] $BackupRoot
)

$ErrorActionPreference = 'Stop'

$moduleRoot = Split-Path -Parent $PSScriptRoot
$spt = (Resolve-Path -LiteralPath $SptRoot).Path
if (-not $BackupRoot) { $BackupRoot = Join-Path $spt 'backups\BAndHB' }
$backupRootPath = [System.IO.Path]::GetFullPath($BackupRoot)
$activeModRoot = [System.IO.Path]::GetFullPath((Join-Path $spt 'SPT_Runtime\user\mods'))
if ($backupRootPath.Equals($activeModRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $backupRootPath.StartsWith($activeModRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "BackupRoot must be outside active server mods: $backupRootPath"
}
if (Get-Process -Name 'SPT.Server','SPT.Launcher','EscapeFromTarkov' -ErrorAction SilentlyContinue) {
    throw 'SPT Server, Launcher and EFT must be closed before HeadBand asset deployment.'
}

$sourceRoot = Join-Path $moduleRoot 'assets\headband-rambo\runtime'
$sourceBundle = (Resolve-Path -LiteralPath (Join-Path $sourceRoot 'bundles\HeadBand\headband_rambo_red.bundle')).Path
$entry = Get-Content -Raw -LiteralPath (Join-Path $sourceRoot 'bundle-entry.json') | ConvertFrom-Json -AsHashtable
$ownedKey = 'HeadBand/headband_rambo_red.bundle'
if ($entry['key'] -ne $ownedKey) { throw "Unexpected HeadBand bundle key: $($entry['key'])" }

$serverRoot = Join-Path $spt 'SPT_Runtime\user\mods\B&A&HB #2 MOD SPT'
$manifestPath = Join-Path $serverRoot 'bundles.json'
$targetBundle = Join-Path $serverRoot 'bundles\HeadBand\headband_rambo_red.bundle'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "B&A&HB bundles.json is missing: $manifestPath" }

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -AsHashtable
$matches = @($manifest['manifest']) | Where-Object { $_['key'] -eq $ownedKey }
if ($matches.Count -gt 1) { throw "B&A&HB bundles.json contains duplicate owned key $ownedKey" }
if ($matches.Count -eq 1 -and (@($matches[0]['dependencyKeys']).Count -ne 0)) {
    throw "Existing owned HeadBand manifest entry has unexpected dependencies; refusing overwrite."
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$backup = Join-Path $backupRootPath "headband-asset-$stamp"
New-Item -ItemType Directory -Force -Path $backup | Out-Null
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $backup 'bundles.json')
if (Test-Path -LiteralPath $targetBundle) {
    Copy-Item -LiteralPath $targetBundle -Destination (Join-Path $backup 'headband_rambo_red.bundle')
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetBundle) | Out-Null
$stagedBundle = "$targetBundle.deploying"
try {
    Copy-Item -LiteralPath $sourceBundle -Destination $stagedBundle -Force
    $sourceHash = (Get-FileHash -LiteralPath $sourceBundle -Algorithm SHA256).Hash
    $stagedHash = (Get-FileHash -LiteralPath $stagedBundle -Algorithm SHA256).Hash
    if ($sourceHash -ne $stagedHash) { throw "HeadBand staged bundle hash mismatch." }

    if ($matches.Count -eq 0) {
        $manifest['manifest'] = @($manifest['manifest']) + @($entry)
        $temporaryManifest = "$manifestPath.deploying"
        $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $temporaryManifest -Encoding utf8NoBOM
        [void](Get-Content -Raw -LiteralPath $temporaryManifest | ConvertFrom-Json -AsHashtable)
        [IO.File]::Move($temporaryManifest, $manifestPath, $true)
    }

    [IO.File]::Move($stagedBundle, $targetBundle, $true)
    $installedHash = (Get-FileHash -LiteralPath $targetBundle -Algorithm SHA256).Hash
    if ($installedHash -ne $sourceHash) { throw "HeadBand installed bundle hash mismatch." }

    [pscustomobject]@{
        Component = 'headband-visual-bundle'
        Path = $targetBundle
        Sha256 = $installedHash.ToLowerInvariant()
        ManifestKey = $ownedKey
        Backup = $backup
    }
}
finally {
    foreach ($temporary in @($stagedBundle, "$manifestPath.deploying")) {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}
