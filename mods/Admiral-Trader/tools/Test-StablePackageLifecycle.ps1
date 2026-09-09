param(
    [Parameter(Mandatory = $true)][string]$CandidateRoot,
    [Parameter(Mandatory = $true)][string]$WorkingDirectory,
    [Parameter(Mandatory = $true)][string]$ExpectedSourceHead
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$candidate = (Resolve-Path $CandidateRoot).Path
$canonicalRelative = 'SPT_Runtime/user/mods/Admiral-Trader'
$canonical = Join-Path $candidate $canonicalRelative
if (-not (Test-Path $canonical -PathType Container)) { throw 'Canonical Admiral-Trader package directory is missing.' }
if (Test-Path (Join-Path $candidate 'SPT_Runtime/user/mods/Admiral Trader')) { throw 'Legacy spaced install directory leaked into the package.' }

foreach ($relative in @(
    'Admiral Trader Server.dll', 'README.md', 'INSTALL.md', 'db/base.json', 'db/assort.json',
    'db/questassort.json', 'manifests/runtime-manifest.json', 'manifests/m6-stable-release.json',
    'assets/d5c27bb3169f8dfbc13f6b69.jpg'
)) {
    if (-not (Test-Path (Join-Path $canonical $relative) -PathType Leaf)) { throw "Required package file is missing: $relative" }
}

$manifest = Get-Content (Join-Path $canonical 'manifests/runtime-manifest.json') -Raw | ConvertFrom-Json
if ($manifest.version -ne '0.1.0+milestones' -or $manifest.sptCompatibility -ne '~4.1.0') { throw 'Staged version/compatibility metadata drifted.' }
if ($manifest.registrationEnabled -ne $true -or $manifest.releaseChannel -ne 'release-candidate' -or $manifest.publicationMode -ne 'stable-release-candidate') { throw 'Staged release gate is not an M6 candidate.' }
if ($manifest.sourceHeadSha -ne $ExpectedSourceHead.ToLowerInvariant()) { throw 'Staged manifest source HEAD mismatch.' }

$provenancePath = Join-Path $candidate 'admiral-trader-provenance.json'
$inventoryPath = Join-Path $candidate 'admiral-trader-package-files.json'
if ((-not (Test-Path $provenancePath -PathType Leaf)) -or (-not (Test-Path $inventoryPath -PathType Leaf))) { throw 'Package provenance or inventory is missing.' }
$provenance = Get-Content $provenancePath -Raw | ConvertFrom-Json
if ($provenance.sourceHeadSha -ne $ExpectedSourceHead.ToLowerInvariant() -or $provenance.version -ne '0.1.0+milestones') { throw 'Package provenance authority mismatch.' }
if ($provenance.questCount -ne 43 -or $provenance.totalFiniteOffers -ne 15) { throw 'Frozen M6 scope drifted.' }

$forbidden = @(Get-ChildItem $candidate -Recurse -File | Where-Object {
    $_.FullName -match '[\\/]user[\\/]profiles[\\/]' -or
    $_.Name -match '^(EscapeFromTarkov|SPTarkov\.|SPT\.Server|BepInEx\.dll)' -or
    $_.Extension -in @('.bak', '.tmp')
})
if ($forbidden.Count) { throw "Forbidden package files: $($forbidden.FullName -join ', ')" }

$sandbox = Join-Path $WorkingDirectory 'install-lifecycle'
if (Test-Path $sandbox) { Remove-Item $sandbox -Recurse -Force }
$mods = Join-Path $sandbox 'SPT_Runtime/user/mods'
$profiles = Join-Path $sandbox 'SPT_Runtime/user/profiles'
New-Item (Join-Path $mods 'Unrelated-Mod') -ItemType Directory -Force | Out-Null
New-Item $profiles -ItemType Directory -Force | Out-Null
'keep' | Set-Content (Join-Path $mods 'Unrelated-Mod/keep.txt')
'profile' | Set-Content (Join-Path $profiles 'profile.json')

# Clean install.
Copy-Item $canonical (Join-Path $mods 'Admiral-Trader') -Recurse
if (-not (Test-Path (Join-Path $mods 'Admiral-Trader/Admiral Trader Server.dll'))) { throw 'Clean install simulation failed.' }

# Upgrade from either historical directory spelling: remove both owned roots, then install once.
New-Item (Join-Path $mods 'Admiral Trader') -ItemType Directory -Force | Out-Null
'stale' | Set-Content (Join-Path $mods 'Admiral Trader/stale.txt')
'stale' | Set-Content (Join-Path $mods 'Admiral-Trader/stale.txt')
foreach ($owned in 'Admiral Trader','Admiral-Trader') {
    $ownedPath = Join-Path $mods $owned
    if (Test-Path $ownedPath) { Remove-Item $ownedPath -Recurse -Force }
}
Copy-Item $canonical (Join-Path $mods 'Admiral-Trader') -Recurse
if ((Test-Path (Join-Path $mods 'Admiral Trader')) -or (Test-Path (Join-Path $mods 'Admiral-Trader/stale.txt'))) { throw 'Upgrade left a duplicate or stale file.' }

# Clean removal must leave profiles and unrelated mods untouched.
Remove-Item (Join-Path $mods 'Admiral-Trader') -Recurse -Force
if (-not (Test-Path (Join-Path $profiles 'profile.json')) -or -not (Test-Path (Join-Path $mods 'Unrelated-Mod/keep.txt'))) { throw 'Clean removal touched unowned data.' }
Write-Host 'Clean install, two-alias upgrade, and clean removal simulations passed.'
