param(
    [Parameter(Mandatory = $true)][string]$PackageRoot,
    [Parameter(Mandatory = $true)][string]$Manifest
)

$ErrorActionPreference = 'Stop'
$contract = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
$allowed = @($contract.allowedPackageFiles | ForEach-Object { $_ -replace '\\', '/' } | Sort-Object)
$actual = @(Get-ChildItem -LiteralPath $PackageRoot -File -Recurse | ForEach-Object {
    [IO.Path]::GetRelativePath((Resolve-Path -LiteralPath $PackageRoot).Path, $_.FullName) -replace '\\', '/'
} | Sort-Object)
$allowedDirs = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($file in $allowed) {
    $parent = [IO.Path]::GetDirectoryName($file) -replace '\\', '/'
    while ($parent) {
        [void]$allowedDirs.Add($parent)
        $parent = [IO.Path]::GetDirectoryName($parent) -replace '\\', '/'
    }
}
$actualDirs = @(Get-ChildItem -LiteralPath $PackageRoot -Directory -Recurse | ForEach-Object {
    [IO.Path]::GetRelativePath((Resolve-Path -LiteralPath $PackageRoot).Path, $_.FullName) -replace '\\', '/'
})

$unexpected = @($actual | Where-Object { $allowed -notcontains $_ })
$missing = @($allowed | Where-Object { $actual -notcontains $_ })
if ($unexpected.Count -gt 0) { throw "Unexpected RC2 package files: $($unexpected -join ', ')" }
if ($missing.Count -gt 0) { throw "Missing RC2 package files: $($missing -join ', ')" }
$unexpectedDirs = @($actualDirs | Where-Object { -not $allowedDirs.Contains($_) })
if ($unexpectedDirs.Count -gt 0) { throw "Unexpected RC2 package directories: $($unexpectedDirs -join ', ')" }

$legacyPattern = '(?i)(^|/)(SPT[ -]Tactical[ -]?HUD)(/|\.dll$)'
$conflicts = @($actual | Where-Object { $_ -match $legacyPattern })
if ($conflicts.Count -gt 0) { throw "Legacy or duplicate Tactical HUD content packaged: $($conflicts -join ', ')" }

$atlases = @($actual | Where-Object { [IO.Path]::GetFileName($_) -ieq 'hud-sprites.png' })
if ($atlases.Count -ne 1 -or $atlases[0] -ne 'BepInEx/plugins/Admiral Tactical HUD/assets/hud-sprites.png') {
    throw "RC2 must contain exactly one canonical atlas; found: $($atlases -join ', ')"
}
Write-Host "[OK] strict RC2 package allowlist validated ($($actual.Count) files)"
