param(
    [Parameter(Mandatory = $true)]
    [string]$SptRoot,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedHeadSha,

    [switch]$Install
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$moduleRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $moduleRoot '..\..')).Path
$builder = Join-Path $PSScriptRoot 'build_spt413_test_candidate.ps1'

if (-not (Test-Path $builder)) {
    throw "Exact-runtime builder is missing: $builder"
}

$expected = $ExpectedHeadSha.Trim().ToLowerInvariant()
if ($expected -notmatch '^[0-9a-f]{40}$') {
    throw 'ExpectedHeadSha must be the full 40-character hexadecimal Git SHA.'
}

& $builder -SptRoot $SptRoot -ExpectedHeadSha $expected -Install:$Install
if ($LASTEXITCODE -ne 0) {
    throw "Exact-runtime candidate builder failed with exit code $LASTEXITCODE"
}

$sourceHead = (& git -C $repoRoot rev-parse HEAD).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $sourceHead -notmatch '^[0-9a-f]{40}$') {
    throw 'Unable to resolve exact source HEAD after candidate build.'
}

if ($sourceHead -ne $expected) {
    throw "Post-build source HEAD mismatch: expected $expected, found $sourceHead"
}

$stageRoot = Join-Path $repoRoot 'build\admiral-trader-test-candidate'
$stageMod = Join-Path $stageRoot 'SPT_Runtime\user\mods\Admiral-Trader'
$provenancePath = Join-Path $stageMod 'candidate-provenance.json'
$manifestPath = Join-Path $stageMod 'manifests\runtime-manifest.json'
$questAssortPath = Join-Path $stageMod 'db\questassort.json'
$serverDllPath = Join-Path $stageMod 'Admiral Trader Server.dll'

foreach ($required in @($provenancePath, $manifestPath, $questAssortPath, $serverDllPath)) {
    if (-not (Test-Path $required)) {
        throw "Exact-runtime staging is incomplete: missing $required"
    }
}

$provenance = Get-Content $provenancePath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($provenance.sourceHeadSha -ne $sourceHead) {
    throw "Candidate provenance source HEAD mismatch: $($provenance.sourceHeadSha) != $sourceHead"
}
if ($provenance.compileMode -ne 'exact-installed-runtime') {
    throw "Refusing to package non-exact candidate compileMode=$($provenance.compileMode)"
}
if ($provenance.publicationMode -ne 'test-candidate') {
    throw "Refusing to package publicationMode=$($provenance.publicationMode)"
}
if ($provenance.physicalRuntimeEvidenceEligible -ne $true) {
    throw 'Refusing to package candidate that is not physical-runtime-evidence eligible.'
}
if ([string]::IsNullOrWhiteSpace([string]$provenance.runtimeCoreSha256) -or $provenance.runtimeCoreSha256 -notmatch '^[0-9a-f]{64}$') {
    throw 'Exact-runtime provenance must contain a 64-hex runtimeCoreSha256.'
}
if ($null -ne $provenance.PSObject.Properties['publishedApiCoreSha256']) {
    throw 'Exact-runtime candidate must not contain publishedApiCoreSha256.'
}
if ([string]::IsNullOrWhiteSpace([string]$provenance.serverDllSha256) -or $provenance.serverDllSha256 -notmatch '^[0-9a-f]{64}$') {
    throw 'Exact-runtime provenance must contain a 64-hex serverDllSha256.'
}

$actualServerDllSha256 = (Get-FileHash $serverDllPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualServerDllSha256 -ne $provenance.serverDllSha256) {
    throw "Staged Admiral server DLL hash drift: provenance=$($provenance.serverDllSha256) actual=$actualServerDllSha256"
}

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.registrationEnabled -ne $true -or $manifest.targetSptVersion -ne '4.1.3' -or $manifest.publicationMode -ne 'test-candidate') {
    throw 'Staged runtime manifest is not an enabled exact SPT 4.1.3 test candidate.'
}

$questAssort = Get-Content $questAssortPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$requiredQuestAssortKeys = @('started', 'success', 'fail')
if ($questAssort.Keys.Count -ne 3 -or @($requiredQuestAssortKeys | Where-Object { -not $questAssort.ContainsKey($_) }).Count -ne 0) {
    throw "Staged questassort must contain exactly native lowercase started/success/fail; found: $($questAssort.Keys -join ', ')"
}
if ($questAssort['success'].Count -ne 7) {
    throw "Staged questassort must retain exactly seven success unlock mappings; found $($questAssort['success'].Count)"
}

$artifactName = "Admiral-Trader-SPT413-$sourceHead.zip"
$artifactPath = Join-Path (Join-Path $repoRoot 'build') $artifactName
$shaPath = "$artifactPath.sha256"
if (Test-Path $artifactPath) { Remove-Item $artifactPath -Force }
if (Test-Path $shaPath) { Remove-Item $shaPath -Force }

Compress-Archive -Path (Join-Path $stageRoot 'SPT_Runtime') -DestinationPath $artifactPath -CompressionLevel Optimal
if (-not (Test-Path $artifactPath)) {
    throw "Exact-runtime candidate archive was not created: $artifactPath"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($artifactPath)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\\', '/') })
    $requiredEntries = @(
        'SPT_Runtime/user/mods/Admiral-Trader/candidate-provenance.json',
        'SPT_Runtime/user/mods/Admiral-Trader/Admiral Trader Server.dll',
        'SPT_Runtime/user/mods/Admiral-Trader/manifests/runtime-manifest.json',
        'SPT_Runtime/user/mods/Admiral-Trader/db/questassort.json'
    )
    foreach ($entry in $requiredEntries) {
        if ($entries -notcontains $entry) {
            throw "Exact-runtime archive layout is invalid; missing $entry"
        }
    }
    if (@($entries | Where-Object { $_ -match '(^|/)(bin|obj)/' -or $_ -match '\.(pdb|log|zip)$' }).Count -ne 0) {
        throw 'Exact-runtime archive contains forbidden build/debug junk.'
    }
}
finally {
    $archive.Dispose()
}

$artifactSha256 = (Get-FileHash $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$artifactSha256  $artifactName" | Set-Content $shaPath -Encoding ascii

Write-Host "Exact-runtime candidate package ready: $artifactPath"
Write-Host "Candidate SHA-256: $artifactSha256"
Write-Host "Source HEAD: $sourceHead"
Write-Host "Installed runtime Server.Core SHA-256: $($provenance.runtimeCoreSha256)"
Write-Host "Admiral server DLL SHA-256: $($provenance.serverDllSha256)"
Write-Host "Install tree inside archive: SPT_Runtime/user/mods/Admiral-Trader"
