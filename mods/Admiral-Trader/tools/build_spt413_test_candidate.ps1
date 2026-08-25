param(
    [Parameter(Mandatory = $true)]
    [string]$SptRoot,

    [string]$ExpectedHeadSha = '',

    [switch]$Install
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$moduleRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $moduleRoot '..\..')).Path
$project = Join-Path $moduleRoot 'server\AdmiralTrader.Server.csproj'
$manifestPath = Join-Path $moduleRoot 'manifests\runtime-manifest.json'

$gitRoot = (& git -C $repoRoot rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitRoot)) {
    throw "Admiral Trader test candidate must be built from a Git checkout so source provenance can be recorded."
}
$gitRoot = (Resolve-Path $gitRoot.Trim()).Path
if ($gitRoot -ne $repoRoot) {
    throw "Resolved Git root does not match repository root: git=$gitRoot repo=$repoRoot"
}

$sourceHead = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceHead -notmatch '^[0-9a-f]{40}$') {
    throw "Unable to resolve a full source HEAD SHA for candidate provenance."
}

$dirty = @(& git -C $repoRoot status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to verify source working-tree state."
}
if ($dirty.Count -ne 0) {
    throw "Refusing to build a runtime candidate from a dirty working tree. Commit/stash all changes first: $($dirty -join '; ')"
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedHeadSha)) {
    $expected = $ExpectedHeadSha.Trim().ToLowerInvariant()
    if ($expected -notmatch '^[0-9a-f]{7,40}$') {
        throw "ExpectedHeadSha must be a 7-40 character hexadecimal Git SHA."
    }
    if (-not $sourceHead.StartsWith($expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Candidate source HEAD mismatch: expected $expected, found $sourceHead"
    }
}

$root = (Resolve-Path $SptRoot).Path
$runtimeRoot = if (Test-Path (Join-Path $root 'SPTarkov.Server.Core.dll')) {
    $root
} elseif (Test-Path (Join-Path $root 'SPT_Runtime\SPTarkov.Server.Core.dll')) {
    Join-Path $root 'SPT_Runtime'
} else {
    throw "Cannot locate SPTarkov.Server.Core.dll. Pass either the SPT game root or its SPT_Runtime directory."
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
    if (-not (Test-Path $path)) {
        throw "Required runtime assembly is missing: $path"
    }
}

$corePath = Join-Path $runtimeRoot 'SPTarkov.Server.Core.dll'
$coreVersion = [System.Reflection.AssemblyName]::GetAssemblyName($corePath).Version
if ($null -eq $coreVersion -or $coreVersion.Major -ne 4 -or $coreVersion.Minor -ne 1 -or $coreVersion.Build -ne 3) {
    throw "Admiral Trader test candidate requires SPTarkov.Server.Core 4.1.3.x; found $coreVersion at $corePath"
}
$coreSha256 = (Get-FileHash $corePath -Algorithm SHA256).Hash.ToLowerInvariant()

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.targetSptVersion -ne '4.1.3') {
    throw "runtime-manifest target drift: $($manifest.targetSptVersion)"
}
if ($manifest.registrationEnabled -ne $false) {
    throw "Source manifest must remain fail-closed; registrationEnabled must be false before staging"
}
if ($manifest.publicationMode -ne 'test-candidate-source') {
    throw "Source manifest publicationMode must be test-candidate-source"
}

Write-Host "Building Admiral Trader source $sourceHead against exact SPT Server.Core $coreVersion from $runtimeRoot"
dotnet build $project -c Release "-p:SptRuntimeLibDir=$runtimeRoot"
if ($LASTEXITCODE -ne 0) {
    throw "Exact SPT 4.1.3 build failed with exit code $LASTEXITCODE"
}

$buildOutput = Join-Path $moduleRoot 'server\bin\Release\net10.0'
$dll = Join-Path $buildOutput 'Admiral Trader Server.dll'
if (-not (Test-Path $dll)) {
    throw "Expected built server DLL is missing: $dll"
}

$stageRoot = Join-Path $repoRoot 'build\admiral-trader-test-candidate'
$stageMod = Join-Path $stageRoot 'SPT_Runtime\user\mods\Admiral-Trader'
if (Test-Path $stageRoot) {
    Remove-Item $stageRoot -Recurse -Force
}
New-Item $stageMod -ItemType Directory -Force | Out-Null

Copy-Item $dll (Join-Path $stageMod 'Admiral Trader Server.dll')
Copy-Item (Join-Path $moduleRoot 'db') (Join-Path $stageMod 'db') -Recurse
Copy-Item (Join-Path $moduleRoot 'manifests') (Join-Path $stageMod 'manifests') -Recurse

$stagedManifestPath = Join-Path $stageMod 'manifests\runtime-manifest.json'
$stagedManifest = Get-Content $stagedManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$stagedManifest.registrationEnabled = $true
$stagedManifest.publicationMode = 'test-candidate'
$stagedManifest | ConvertTo-Json -Depth 20 | Set-Content $stagedManifestPath -Encoding UTF8

$provenance = [ordered]@{
    schemaVersion = 1
    product = 'Admiral Trader'
    sourceHeadSha = $sourceHead
    sourceTreeClean = $true
    targetSptVersion = '4.1.3'
    runtimeCoreVersion = $coreVersion.ToString()
    runtimeCoreSha256 = $coreSha256
    serverDllSha256 = (Get-FileHash (Join-Path $stageMod 'Admiral Trader Server.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
}
$provenance | ConvertTo-Json | Set-Content (Join-Path $stageMod 'candidate-provenance.json') -Encoding UTF8

$junk = Get-ChildItem $stageRoot -Recurse -File | Where-Object {
    $_.Extension -in @('.pdb', '.log', '.zip') -or $_.Name -match '(^|[-_.])(tmp|temp)([-_.]|$)'
}
if ($junk) {
    throw "Candidate staging contains forbidden temporary/debug artifacts: $($junk.FullName -join ', ')"
}

$stagedManifest = Get-Content $stagedManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($stagedManifest.registrationEnabled -ne $true -or $stagedManifest.targetSptVersion -ne '4.1.3' -or $stagedManifest.publicationMode -ne 'test-candidate') {
    throw "Staged candidate manifest is not an enabled SPT 4.1.3 test candidate"
}

$stagedProvenance = Get-Content (Join-Path $stageMod 'candidate-provenance.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($stagedProvenance.sourceHeadSha -ne $sourceHead -or $stagedProvenance.runtimeCoreSha256 -ne $coreSha256) {
    throw "Staged candidate provenance does not match the verified source/runtime inputs."
}

Write-Host "Candidate staged at: $stageRoot"
Write-Host "Candidate provenance: source=$sourceHead runtimeCore=$coreVersion runtimeCoreSha256=$coreSha256"

if ($Install) {
    $destination = Join-Path $runtimeRoot 'user\mods\Admiral-Trader'
    if (Test-Path $destination) {
        Remove-Item $destination -Recurse -Force
    }
    New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
    Copy-Item $stageMod $destination -Recurse
    Write-Host "Installed test candidate to: $destination"
}
