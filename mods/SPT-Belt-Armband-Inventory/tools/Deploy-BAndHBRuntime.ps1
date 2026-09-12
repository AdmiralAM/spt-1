[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $SptRoot,

    [Parameter(Mandatory = $true)]
    [string] $ClientDll,

    [Parameter(Mandatory = $true)]
    [string] $ServerDll,

    [string] $BackupRoot
)

$ErrorActionPreference = 'Stop'

$resolvedSptRoot = (Resolve-Path -LiteralPath $SptRoot).Path
$resolvedClientDll = (Resolve-Path -LiteralPath $ClientDll).Path
$resolvedServerDll = (Resolve-Path -LiteralPath $ServerDll).Path

if (-not $BackupRoot) {
    $BackupRoot = Join-Path $resolvedSptRoot 'backups\BAndHB'
}

$clientTarget = Join-Path $resolvedSptRoot 'BepInEx\plugins\Admiral SPT\SPT Belt Armband Inventory v0.1.0.dll'
$serverTarget = Join-Path $resolvedSptRoot 'SPT_Runtime\user\mods\B&A&HB #2 MOD SPT\SPT-Belt-Armband-Inventory.Server.dll'
$clientDirectory = Split-Path -Parent $clientTarget
$serverDirectory = Split-Path -Parent $serverTarget
$resolvedBackupRoot = [System.IO.Path]::GetFullPath($BackupRoot)

$activeRoots = @(
    [System.IO.Path]::GetFullPath((Join-Path $resolvedSptRoot 'BepInEx\plugins')),
    [System.IO.Path]::GetFullPath((Join-Path $resolvedSptRoot 'SPT_Runtime\user\mods'))
)
foreach ($activeRoot in $activeRoots) {
    if ($resolvedBackupRoot.StartsWith($activeRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
        $resolvedBackupRoot.Equals($activeRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "BackupRoot must be outside active mod/plugin directories: $resolvedBackupRoot"
    }
}

New-Item -ItemType Directory -Force -Path $clientDirectory, $serverDirectory, $resolvedBackupRoot | Out-Null

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$backupDirectory = Join-Path $resolvedBackupRoot $timestamp
New-Item -ItemType Directory -Path $backupDirectory | Out-Null

$targets = @(
    [pscustomobject]@{ Name = 'client'; Source = $resolvedClientDll; Target = $clientTarget },
    [pscustomobject]@{ Name = 'server'; Source = $resolvedServerDll; Target = $serverTarget }
)

foreach ($entry in $targets) {
    if (Test-Path -LiteralPath $entry.Target) {
        Copy-Item -LiteralPath $entry.Target -Destination (Join-Path $backupDirectory "$($entry.Name).dll")
    }
}

try {
    foreach ($entry in $targets) {
        $expectedHash = (Get-FileHash -LiteralPath $entry.Source -Algorithm SHA256).Hash
        $temporaryTarget = "$($entry.Target).deploying"
        Copy-Item -LiteralPath $entry.Source -Destination $temporaryTarget -Force
        $stagedHash = (Get-FileHash -LiteralPath $temporaryTarget -Algorithm SHA256).Hash
        if ($stagedHash -ne $expectedHash) {
            throw "$($entry.Name) staged hash mismatch: expected $expectedHash, got $stagedHash"
        }
        [System.IO.File]::Move($temporaryTarget, $entry.Target, $true)
        $installedHash = (Get-FileHash -LiteralPath $entry.Target -Algorithm SHA256).Hash
        if ($installedHash -ne $expectedHash) {
            throw "$($entry.Name) installed hash mismatch: expected $expectedHash, got $installedHash"
        }
        [pscustomobject]@{
            Component = $entry.Name
            Path = $entry.Target
            Sha256 = $installedHash.ToLowerInvariant()
            Backup = $backupDirectory
        }
    }
}
finally {
    foreach ($entry in $targets) {
        $temporaryTarget = "$($entry.Target).deploying"
        if (Test-Path -LiteralPath $temporaryTarget) {
            Remove-Item -LiteralPath $temporaryTarget -Force
        }
    }
}
