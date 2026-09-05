param(
    [Parameter(Mandatory = $true)][string]$SptRoot,
    [Parameter(Mandatory = $true)][string]$TraderWorktree,
    [Parameter(Mandatory = $true)][string]$SourceHeadSha,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$economyRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $economyRoot '../..')).Path
$runtimeRoot = if (Test-Path (Join-Path $SptRoot 'SPTarkov.Server.Core.dll')) {
    (Resolve-Path $SptRoot).Path
} elseif (Test-Path (Join-Path $SptRoot 'SPT_Runtime/SPTarkov.Server.Core.dll')) {
    (Resolve-Path (Join-Path $SptRoot 'SPT_Runtime')).Path
} else { throw "SPT 4.1.3 runtime root was not found below $SptRoot" }

$expectedRuntime = [ordered]@{
    'SPTarkov.Server.Core.dll' = '9db58535db2c2d2192980704b526bc0979006db27d833f39f7907b5803101905'
    'SPTarkov.Common.dll' = '4e5c2e3286c07f13121974c101b58b29f9598114e0ed30f42988b702833e5081'
    'SPTarkov.DI.dll' = 'd7515b2ba613d9bc4dc830d7f77dff27e7ad97f0b32cf77f772ded55882a982b'
    'SemanticVersioning.dll' = '1ec4e9d7312678e23e40724207d871d0dd68a9518e39fe8165beb6e5f98b0961'
    'JetBrains.Annotations.dll' = '0295966b8d44522eec6a3c560fbe4efcfa878d3e16ff1ab0fb3478ab17001381'
}
$runtimeHashes = [ordered]@{}
foreach ($entry in $expectedRuntime.GetEnumerator()) {
    $path = Join-Path $runtimeRoot $entry.Key
    if (-not (Test-Path $path -PathType Leaf)) { throw "Required runtime DLL is missing: $path" }
    $actual = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.Value) { throw "SPT runtime hash mismatch for $($entry.Key): $actual" }
    $runtimeHashes[$entry.Key] = $actual
}

$sourceHead = (& git -C $repoRoot rev-parse HEAD).Trim().ToLowerInvariant()
$traderHead = (& git -C $TraderWorktree rev-parse HEAD).Trim().ToLowerInvariant()
if ($sourceHead -ne $SourceHeadSha.ToLowerInvariant()) { throw "Economy head mismatch: $sourceHead" }
if ($traderHead -ne '053a62ff5f1cb545f13bc89a96bba3acd319a823') { throw "Frozen Trader head mismatch: $traderHead" }
if (& git -C $TraderWorktree status --porcelain) { throw 'Frozen Trader worktree must be clean.' }

& (Join-Path $TraderWorktree 'mods/Admiral-Trader/tools/package_spt413_exact_candidate.ps1') -SptRoot $runtimeRoot -ExpectedHeadSha $traderHead
if ($LASTEXITCODE -ne 0) { throw 'Frozen Trader exact-runtime packaging failed.' }
$traderStage = Join-Path $TraderWorktree 'build/admiral-trader-test-candidate/SPT_Runtime/user/mods/Admiral-Trader'

dotnet build (Join-Path $economyRoot 'server/Economy-Admiral.csproj') -c Release --nologo "-p:SptRuntimeLibDir=$runtimeRoot"
if ($LASTEXITCODE -ne 0) { throw 'Economy exact-runtime server build failed.' }
dotnet build (Join-Path $economyRoot 'client/Economy-Admiral.Client.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Economy F12 client build failed.' }

$packageRoot = Join-Path $OutputDirectory "Admiral-Trader-Economy-SPT413-RC-$sourceHead"
if (Test-Path $packageRoot) { Remove-Item $packageRoot -Recurse -Force }
$traderTarget = Join-Path $packageRoot 'SPT_Runtime/user/mods/Admiral-Trader'
$economyTarget = Join-Path $packageRoot 'SPT_Runtime/user/mods/Economy Admiral'
$clientTarget = Join-Path $packageRoot 'BepInEx/plugins/Economy Admiral'
New-Item (Join-Path $economyTarget 'config') -ItemType Directory -Force | Out-Null
New-Item $clientTarget -ItemType Directory -Force | Out-Null
Copy-Item $traderStage $traderTarget -Recurse
Copy-Item (Join-Path $economyRoot 'server/bin/Release/net10.0/Economy-Admiral.dll') (Join-Path $economyTarget 'Economy-Admiral.dll')
Copy-Item (Join-Path $economyRoot 'client/bin/Release/netstandard2.1/Economy Admiral v0.1.0.dll') (Join-Path $clientTarget 'Economy Admiral v0.1.0.dll')
Copy-Item (Join-Path $economyRoot 'config/config.json') (Join-Path $economyTarget 'config/config.default.json')
Copy-Item (Join-Path $economyRoot 'README.md') (Join-Path $economyTarget 'README.md')

$quests = @(Get-ChildItem (Join-Path $traderTarget 'db/quests') -Filter '*.json' -File)
$assort = Get-Content (Join-Path $traderTarget 'db/assort.json') -Raw | ConvertFrom-Json
$baseline = Get-Content (Join-Path $traderTarget 'manifests/baseline-stock.json') -Raw | ConvertFrom-Json
$questAssort = Get-Content (Join-Path $traderTarget 'db/questassort.json') -Raw | ConvertFrom-Json
$rootOfferIds = @($assort.items | Where-Object parentId -eq 'hideout' | ForEach-Object { [string]$_."_id" })
$baselineIds = @($baseline.offers | ForEach-Object { [string]$_.offerId })
$milestoneIds = @($questAssort.success.PSObject.Properties | ForEach-Object { [string]$_.Value })
if ($quests.Count -ne 31) { throw "Trader quest count drift: $($quests.Count)" }
if ($rootOfferIds.Count -ne 11) { throw 'Trader root offer count drift.' }
if ($baselineIds.Count -ne 4) { throw 'Trader Baseline count drift.' }
if ($milestoneIds.Count -ne 7) { throw 'Trader Milestone count drift.' }
if (@($rootOfferIds | Where-Object { $_ -notin $baselineIds -and $_ -notin $milestoneIds }).Count -ne 0) { throw 'Relationship or unclassified offers must not materialize.' }

$config = Get-Content (Join-Path $economyTarget 'config/config.default.json') -Raw | ConvertFrom-Json
if ($config.mode -ne 'Enforce' -or $config.preset -ne 'Normal') { throw 'Economy candidate must default to Normal/Enforce.' }
foreach ($cluster in 'enableQuestEconomyCluster','enableTraderEconomyCluster','enableFleaEconomyCluster','enableLootEconomyCluster') {
    if ($config.$cluster -ne $true) { throw "Economy cluster must default enabled: $cluster" }
}

$manifest = [ordered]@{
    schemaVersion = 1; product = 'Admiral Trader 0.1.0 + Economy Admiral 0.1.0'
    targetSptVersion = '4.1.3'; sourceHeadSha = $sourceHead; supersedesPullRequest = 325
    frozenTrader = [ordered]@{ sourceHeadSha = $traderHead; traderId = 'd5c27bb3169f8dfbc13f6b69'; questCount = 31; baselineOffers = 4; milestoneOffers = 7; relationshipOffers = 0 }
    economy = [ordered]@{ version = '0.1.0'; defaultMode = 'Enforce'; recommendedPreset = 'Normal'; standalone = $true; ownsTraderEngine = $false }
    runtimeDllSha256 = $runtimeHashes
    ownedFilesOnly = $true
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $packageRoot 'combined-provenance.json') -Encoding utf8

$foreign = @(Get-ChildItem $packageRoot -Recurse -File | Where-Object { $_.Name -in $expectedRuntime.Keys -or $_.Name -match '^(EscapeFromTarkov|SPT\.Server)' })
if ($foreign.Count) { throw "Foreign SPT/EFT binaries leaked into package: $($foreign.FullName -join ', ')" }
$zip = "$packageRoot.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zip -CompressionLevel Optimal
$zipHash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$zipHash  $([IO.Path]::GetFileName($zip))" | Set-Content "$zip.sha256" -Encoding ascii
Write-Host "Combined candidate: $zip"
Write-Host "Combined candidate SHA-256: $zipHash"
