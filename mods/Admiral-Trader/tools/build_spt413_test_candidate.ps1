param(
    [Parameter(Mandatory = $true)]
    [string]$SptRoot,

    [switch]$Install
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$moduleRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $moduleRoot '..\..')).Path
$project = Join-Path $moduleRoot 'server\AdmiralTrader.Server.csproj'
$manifestPath = Join-Path $moduleRoot 'manifests\runtime-manifest.json'

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

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.targetSptVersion -ne '4.1.3') {
    throw "runtime-manifest target drift: $($manifest.targetSptVersion)"
}
if ($manifest.registrationEnabled -ne $true) {
    throw "runtime-manifest publication gate is disabled; this branch is not yet a runnable test candidate"
}
if ($manifest.publicationMode -ne 'test-candidate') {
    throw "runtime-manifest publicationMode must be test-candidate for this builder"
}

Write-Host "Building Admiral Trader against exact SPT Server.Core $coreVersion from $runtimeRoot"
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

$junk = Get-ChildItem $stageRoot -Recurse -File | Where-Object {
    $_.Extension -in @('.pdb', '.log', '.zip') -or $_.Name -match '(^|[-_.])(tmp|temp)([-_.]|$)'
}
if ($junk) {
    throw "Candidate staging contains forbidden temporary/debug artifacts: $($junk.FullName -join ', ')"
}

$stagedManifest = Get-Content (Join-Path $stageMod 'manifests\runtime-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($stagedManifest.registrationEnabled -ne $true -or $stagedManifest.targetSptVersion -ne '4.1.3') {
    throw "Staged candidate manifest is not an enabled SPT 4.1.3 test candidate"
}

Write-Host "Candidate staged at: $stageRoot"

if ($Install) {
    $destination = Join-Path $runtimeRoot 'user\mods\Admiral-Trader'
    if (Test-Path $destination) {
        Remove-Item $destination -Recurse -Force
    }
    New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
    Copy-Item $stageMod $destination -Recurse
    Write-Host "Installed test candidate to: $destination"
}
