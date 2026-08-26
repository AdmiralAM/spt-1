param(
    [Parameter(Mandatory = $true)]
    [string]$SptRoot,

    [string]$ExpectedHeadSha = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$moduleRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $moduleRoot '..\..')).Path
$project = Join-Path $moduleRoot 'server\AdmiralTrader.Server.csproj'
$manifestPath = Join-Path $moduleRoot 'manifests\runtime-manifest.json'
$identityPath = Join-Path $moduleRoot 'manifests\identity-assets.json'

$gitRoot = (& git -C $repoRoot rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitRoot)) {
    throw 'Admiral Trader test candidate must be built from a Git checkout so source provenance can be recorded.'
}
$gitRoot = (Resolve-Path $gitRoot.Trim()).Path
if ($gitRoot -ne $repoRoot) {
    throw "Resolved Git root does not match repository root: git=$gitRoot repo=$repoRoot"
}

$sourceHead = (& git -C $repoRoot rev-parse HEAD).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $sourceHead -notmatch '^[0-9a-f]{40}$') {
    throw 'Unable to resolve a full source HEAD SHA for candidate provenance.'
}

$dirty = @(& git -C $repoRoot status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) { throw 'Unable to verify source working-tree state.' }
if ($dirty.Count -ne 0) {
    throw "Refusing to build a runtime candidate from a dirty working tree. Commit/stash all changes first: $($dirty -join '; ')"
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedHeadSha)) {
    $expected = $ExpectedHeadSha.Trim().ToLowerInvariant()
    if ($expected -notmatch '^[0-9a-f]{40}$') { throw 'ExpectedHeadSha must be the full 40-character hexadecimal Git SHA.' }
    if ($sourceHead -ne $expected) { throw "Candidate source HEAD mismatch: expected $expected, found $sourceHead" }
}

$root = (Resolve-Path $SptRoot).Path
$runtimeRoot = if (Test-Path (Join-Path $root 'SPTarkov.Server.Core.dll')) {
    $root
} elseif (Test-Path (Join-Path $root 'SPT_Runtime\SPTarkov.Server.Core.dll')) {
    Join-Path $root 'SPT_Runtime'
} else {
    throw 'Cannot locate SPTarkov.Server.Core.dll. Pass either the SPT game root or its SPT_Runtime directory.'
}

$requiredRuntimeAssemblies = @(
    'SPTarkov.Server.Core.dll',
    'SPTarkov.Common.dll',
    'SPTarkov.DI.dll',
    'SemanticVersioning.dll',
    'JetBrains.Annotations.dll'
)
foreach ($assembly in $requiredRuntimeAssemblies) {
    $path = Join-Path $runtimeRoot $assembly
    if (-not (Test-Path $path)) { throw "Required runtime assembly is missing: $path" }
}

$corePath = Join-Path $runtimeRoot 'SPTarkov.Server.Core.dll'
$coreVersion = [System.Reflection.AssemblyName]::GetAssemblyName($corePath).Version
if ($null -eq $coreVersion -or $coreVersion.Major -ne 4 -or $coreVersion.Minor -ne 1 -or $coreVersion.Build -ne 3) {
    throw "Admiral Trader test candidate requires SPTarkov.Server.Core 4.1.3.x; found $coreVersion at $corePath"
}
$coreSha256 = (Get-FileHash $corePath -Algorithm SHA256).Hash.ToLowerInvariant()

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.targetSptVersion -ne '4.1.3') { throw "runtime-manifest target drift: $($manifest.targetSptVersion)" }
if ($manifest.registrationEnabled -ne $false) { throw 'Source manifest must remain fail-closed; registrationEnabled must be false before staging' }
if ($manifest.publicationMode -ne 'test-candidate-source') { throw 'Source manifest publicationMode must be test-candidate-source' }

$identity = Get-Content $identityPath -Raw -Encoding UTF8 | ConvertFrom-Json
$portraitRelative = [string]$identity.portrait.runtimeAsset
$portraitSource = Join-Path $moduleRoot ($portraitRelative -replace '/', '\')
if (-not (Test-Path $portraitSource)) { throw "Official portrait is missing from source tree: $portraitRelative" }

$portraitGitBlobSha1 = (& git -C $repoRoot hash-object $portraitSource).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $portraitGitBlobSha1 -notmatch '^[0-9a-f]{40}$') {
    throw 'Unable to calculate official portrait Git blob identity.'
}
if ($portraitGitBlobSha1 -ne [string]$identity.portrait.runtimeGitBlobSha1) {
    throw "Official portrait hash drift: manifestGitBlob=$($identity.portrait.runtimeGitBlobSha1) actualGitBlob=$portraitGitBlobSha1"
}
$portraitSha256 = (Get-FileHash $portraitSource -Algorithm SHA256).Hash.ToLowerInvariant()
$portraitBytes = [System.IO.File]::ReadAllBytes($portraitSource)
if ($portraitBytes.Length -lt 4 -or $portraitBytes[0] -ne 0xFF -or $portraitBytes[1] -ne 0xD8 -or $portraitBytes[$portraitBytes.Length - 2] -ne 0xFF -or $portraitBytes[$portraitBytes.Length - 1] -ne 0xD9) {
    throw 'Official portrait runtime asset is not a complete JPEG stream.'
}

Write-Host "Building Admiral Trader source $sourceHead against exact installed SPT Server.Core $coreVersion from $runtimeRoot"
dotnet build $project -c Release "-p:SptRuntimeLibDir=$runtimeRoot"
if ($LASTEXITCODE -ne 0) { throw "Exact SPT 4.1.3 build failed with exit code $LASTEXITCODE" }

$buildOutput = Join-Path $moduleRoot 'server\bin\Release\net10.0'
$dll = Join-Path $buildOutput 'Admiral Trader Server.dll'
if (-not (Test-Path $dll)) { throw "Expected built server DLL is missing: $dll" }

$stageRoot = Join-Path $repoRoot 'build\admiral-trader-test-candidate'
$stageMod = Join-Path $stageRoot 'SPT_Runtime\user\mods\Admiral-Trader'
if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
New-Item $stageMod -ItemType Directory -Force | Out-Null

Copy-Item $dll (Join-Path $stageMod 'Admiral Trader Server.dll')
Copy-Item (Join-Path $moduleRoot 'db') (Join-Path $stageMod 'db') -Recurse
Copy-Item (Join-Path $moduleRoot 'manifests') (Join-Path $stageMod 'manifests') -Recurse
Copy-Item (Join-Path $moduleRoot 'assets') (Join-Path $stageMod 'assets') -Recurse

$stagedManifestPath = Join-Path $stageMod 'manifests\runtime-manifest.json'
$stagedManifest = Get-Content $stagedManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$stagedManifest.registrationEnabled = $true
$stagedManifest.publicationMode = 'test-candidate'
$stagedManifest | ConvertTo-Json -Depth 20 | Set-Content $stagedManifestPath -Encoding UTF8

$stagedQuestAssortPath = Join-Path $stageMod 'db\questassort.json'
$stagedQuestAssort = Get-Content $stagedQuestAssortPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$requiredQuestAssortKeys = @('started', 'success', 'fail')
$actualQuestAssortKeys = @($stagedQuestAssort.Keys)
if ($actualQuestAssortKeys.Count -ne $requiredQuestAssortKeys.Count) { throw "Staged questassort must contain exactly started/success/fail; found: $($actualQuestAssortKeys -join ', ')" }
foreach ($key in $requiredQuestAssortKeys) {
    if (-not $stagedQuestAssort.ContainsKey($key)) { throw "Staged questassort is missing native lowercase key '$key'" }
}
if ($stagedQuestAssort['success'].Count -ne 7) { throw "Staged questassort must retain exactly seven success unlock mappings; found $($stagedQuestAssort['success'].Count)" }

$stagedPortrait = Join-Path $stageMod ($portraitRelative -replace '/', '\')
if (-not (Test-Path $stagedPortrait)) { throw "Staged candidate is missing official portrait: $portraitRelative" }
$stagedPortraitSha256 = (Get-FileHash $stagedPortrait -Algorithm SHA256).Hash.ToLowerInvariant()
if ($stagedPortraitSha256 -ne $portraitSha256) { throw "Staged official portrait hash drift: source=$portraitSha256 staged=$stagedPortraitSha256" }

$provenance = [ordered]@{
    schemaVersion = 4
    product = 'Admiral Trader'
    sourceHeadSha = $sourceHead
    sourceTreeClean = $true
    targetSptVersion = '4.1.3'
    compileMode = 'exact-installed-runtime'
    runtimeAssemblyIdentity = 'SPTarkov.Server.Core.dll'
    runtimeCoreVersion = $coreVersion.ToString()
    runtimeCoreSha256 = $coreSha256
    serverDllSha256 = (Get-FileHash (Join-Path $stageMod 'Admiral Trader Server.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    officialPortraitRoute = [string]$identity.portrait.runtimeRoute
    officialPortraitGitBlobSha1 = $portraitGitBlobSha1
    officialPortraitSha256 = $portraitSha256
    publicationMode = 'test-candidate'
    physicalRuntimeEvidenceEligible = $true
}
$provenance | ConvertTo-Json | Set-Content (Join-Path $stageMod 'candidate-provenance.json') -Encoding UTF8

$junk = Get-ChildItem $stageRoot -Recurse -File | Where-Object {
    $_.Extension -in @('.pdb', '.log', '.zip') -or $_.Name -match '(^|[-_.])(tmp|temp)([-_.]|$)'
}
if ($junk) { throw "Candidate staging contains forbidden temporary/debug artifacts: $($junk.FullName -join ', ')" }

$stagedManifest = Get-Content $stagedManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($stagedManifest.registrationEnabled -ne $true -or $stagedManifest.targetSptVersion -ne '4.1.3' -or $stagedManifest.publicationMode -ne 'test-candidate') {
    throw 'Staged candidate manifest is not an enabled SPT 4.1.3 test candidate'
}

$stagedProvenance = Get-Content (Join-Path $stageMod 'candidate-provenance.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (
    $stagedProvenance.sourceHeadSha -ne $sourceHead -or
    $stagedProvenance.compileMode -ne 'exact-installed-runtime' -or
    $stagedProvenance.runtimeCoreSha256 -ne $coreSha256 -or
    $stagedProvenance.officialPortraitGitBlobSha1 -ne $portraitGitBlobSha1 -or
    $stagedProvenance.officialPortraitSha256 -ne $portraitSha256 -or
    $stagedProvenance.physicalRuntimeEvidenceEligible -ne $true
) {
    throw 'Staged candidate provenance does not match the verified source/runtime inputs.'
}

Write-Host "Candidate staged at: $stageRoot"
Write-Host "Candidate provenance: source=$sourceHead compileMode=exact-installed-runtime runtimeCore=$coreVersion runtimeCoreSha256=$coreSha256 portraitGitBlob=$portraitGitBlobSha1 portraitSha256=$portraitSha256"
Write-Host 'Staging-only builder completed. Use package_spt413_exact_candidate.ps1 for validated archive creation and optional rollback-safe installation.'
