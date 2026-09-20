param(
    [Parameter(Mandatory=$true)][string]$Destination,
    [string]$SourceRuntime,
    [string]$WorkingDirectory
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$inventory = Get-Content (Join-Path $root 'manifests/artem-bundle-inventory.json') -Raw | ConvertFrom-Json
$overrideRoot = Join-Path $root 'tools/artem-bundle-overrides'
$destinationRoot = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force -Path $destinationRoot | Out-Null

if ($SourceRuntime) {
    $sourceRoot = (Resolve-Path -LiteralPath $SourceRuntime).Path
} else {
    if (-not $WorkingDirectory) { $WorkingDirectory = Join-Path ([IO.Path]::GetTempPath()) 'admiral-artem-bundles' }
    New-Item -ItemType Directory -Force -Path $WorkingDirectory | Out-Null
    $archive = Join-Path $WorkingDirectory $inventory.sourceAsset
    if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) {
        Invoke-WebRequest -Uri 'https://github.com/WelcomeToThursday/WTT-Artem/releases/download/3.0.2/WTT-Artem.7z' -OutFile $archive
    }
    $archiveHash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($archiveHash -ne $inventory.sourceAssetSha256) { throw "Artem source asset hash mismatch: $archiveHash" }
    $extract = Join-Path $WorkingDirectory 'extracted'
    if (Test-Path -LiteralPath $extract) { Remove-Item -LiteralPath $extract -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $extract | Out-Null
    $sevenZipCommand = Get-Command 7z.exe -ErrorAction SilentlyContinue
    $sevenZip = if ($null -ne $sevenZipCommand) { $sevenZipCommand.Source } else { 'C:\Program Files\7-Zip\7z.exe' }
    if (Test-Path -LiteralPath $sevenZip -PathType Leaf) {
        & $sevenZip x $archive "-o$extract" -y | Out-Null
    } else {
        $tarCommand = Get-Command tar.exe -ErrorAction SilentlyContinue
        if ($null -eq $tarCommand) { throw '7z.exe or tar.exe is required to hydrate the pinned Artem asset' }
        & $tarCommand.Source -xf $archive -C $extract
    }
    if ($LASTEXITCODE -ne 0) { throw 'Failed to extract the pinned Artem asset' }
    $sourceRoot = (Get-ChildItem $extract -Recurse -Directory | Where-Object {
        (Test-Path (Join-Path $_.FullName 'artemvest10.bundle')) -or
        ((Split-Path $_.FullName -Leaf) -eq 'bundles' -and (Get-ChildItem $_.FullName -Filter '*.bundle' -File -ErrorAction SilentlyContinue))
    } | Select-Object -First 1).FullName
    if (-not $sourceRoot) { throw 'Extracted Artem bundle directory was not found' }
}

$copied = 0
foreach ($entry in $inventory.bundles) {
    $relative = [string]$entry.path
    $source = Join-Path $sourceRoot $relative
    $target = Join-Path $destinationRoot $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Artem bundle is missing: $relative" }
    $hash = (Get-FileHash $source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $entry.sha256 -or (Get-Item $source).Length -ne $entry.size) {
        $override = Join-Path $overrideRoot $relative
        if (-not (Test-Path -LiteralPath $override -PathType Leaf)) { throw "Artem bundle contract drift without a verified override: $relative" }
        $overrideHash = (Get-FileHash $override -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($overrideHash -ne $entry.sha256 -or (Get-Item $override).Length -ne $entry.size) { throw "Artem bundle override contract drift: $relative" }
        $source = $override
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    $copied++
}
if ($copied -ne $inventory.bundleCount) { throw "Expected $($inventory.bundleCount) Artem bundles, copied $copied" }
Write-Host "Hydrated $copied verified Artem bundles into $destinationRoot"
