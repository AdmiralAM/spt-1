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
} else { throw "SPT 4.1.5 runtime root was not found below $SptRoot" }

$expectedRuntime = [ordered]@{
    'SPTarkov.Server.Core.dll' = 'c502d59b03c625e918efb4cea5f836d26fc6d99d0a5be1df38c90f0a8098ec88'
    'SPTarkov.Common.dll' = '66554b9a7515362f0afc8cde58cd630e6bb0471921b3aa16dbaf191428c635d2'
    'SPTarkov.DI.dll' = '4aa9d16678d1cbe59f80294115be4570e579b93b1f3aadea6c460232e517904d'
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

$traderModule = Join-Path $TraderWorktree 'mods/Admiral-Trader'
$migrationOverlay = Join-Path $OutputDirectory 'trader-spt415-overlay'
New-Item $migrationOverlay -ItemType Directory -Force | Out-Null
$overlayMetadata = Join-Path $migrationOverlay 'ModMetadata.Spt415.cs'
$overlayRegistration = Join-Path $migrationOverlay 'TraderRegistration.Spt415.cs'
$overlayTargets = Join-Path $migrationOverlay 'TraderSpt415.targets'
(Get-Content (Join-Path $traderModule 'server/ModMetadata.cs') -Raw).Replace('new("4.1.3")', 'new("4.1.5")') | Set-Content $overlayMetadata -Encoding utf8
(Get-Content (Join-Path $traderModule 'server/TraderRegistration.cs') -Raw).Replace('"4.1.3"', '"4.1.5"') | Set-Content $overlayRegistration -Encoding utf8
@"
<Project>
  <Target Name="ApplyFrozenTraderSpt415MetadataOverlay" BeforeTargets="CoreCompile">
    <ItemGroup>
      <Compile Remove="ModMetadata.cs;TraderRegistration.cs;$($traderModule.Replace('\','/'))/server/ModMetadata.cs;$($traderModule.Replace('\','/'))/server/TraderRegistration.cs" />
      <Compile Include="$($overlayMetadata.Replace('\','/'))" />
      <Compile Include="$($overlayRegistration.Replace('\','/'))" />
    </ItemGroup>
  </Target>
</Project>
"@ | Set-Content $overlayTargets -Encoding utf8
dotnet build (Join-Path $traderModule 'server/AdmiralTrader.Server.csproj') -c Release --nologo "-p:SptRuntimeLibDir=$runtimeRoot" "-p:CustomAfterMicrosoftCommonTargets=$overlayTargets"
if ($LASTEXITCODE -ne 0) { throw 'Frozen Trader exact-runtime build failed.' }
if (& git -C $TraderWorktree status --porcelain) { throw 'Frozen Trader source changed while applying the SPT 4.1.5 packaging overlay.' }
$traderStage = Join-Path $OutputDirectory 'frozen-trader-stage/SPT_Runtime/user/mods/Admiral-Trader'
if (Test-Path $traderStage) { Remove-Item $traderStage -Recurse -Force }
New-Item $traderStage -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $traderModule 'server/bin/Release/net10.0/Admiral Trader Server.dll') $traderStage
foreach ($directory in 'db','manifests','assets') { Copy-Item (Join-Path $traderModule $directory) (Join-Path $traderStage $directory) -Recurse }
python (Join-Path $PSScriptRoot 'prepare_fresh_profile_quest.py') $traderStage
if ($LASTEXITCODE -ne 0) { throw 'Fresh-profile quest staging failed.' }
New-Item (Join-Path $traderStage 'tools') -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $traderModule 'tools/Reset-AdmiralTraderProfile.ps1') (Join-Path $traderStage 'tools/Reset-AdmiralTraderProfile.ps1')
$stagedTraderManifestPath = Join-Path $traderStage 'manifests/runtime-manifest.json'
$stagedTraderManifest = Get-Content $stagedTraderManifestPath -Raw | ConvertFrom-Json
$stagedTraderManifest.targetSptVersion = '4.1.5'
$stagedTraderManifest.registrationEnabled = $true
$stagedTraderManifest.publicationMode = 'test-candidate'
$stagedTraderManifest | ConvertTo-Json -Depth 20 | Set-Content $stagedTraderManifestPath -Encoding utf8

dotnet build (Join-Path $economyRoot 'server/Economy-Admiral.csproj') -c Release --nologo "-p:SptRuntimeLibDir=$runtimeRoot"
if ($LASTEXITCODE -ne 0) { throw 'Economy exact-runtime server build failed.' }
dotnet build (Join-Path $economyRoot 'client/Economy-Admiral.Client.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Economy F12 client build failed.' }

$packageRoot = Join-Path $OutputDirectory "Admiral-Trader-Economy-SPT415-RC-$sourceHead"
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
$milestoneIds = @($questAssort.success.PSObject.Properties | ForEach-Object { [string]$_.Name })
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
    targetSptVersion = '4.1.5'; sourceHeadSha = $sourceHead; supersedesPullRequest = 325
    frozenTrader = [ordered]@{ sourceHeadSha = $traderHead; traderId = 'd5c27bb3169f8dfbc13f6b69'; questCount = 31; baselineOffers = 4; milestoneOffers = 7; relationshipOffers = 0 }
    economy = [ordered]@{ version = '0.1.0'; defaultMode = 'Enforce'; recommendedPreset = 'Normal'; standalone = $true; ownsTraderEngine = $false }
    runtimeRelease = [ordered]@{ archive = 'SPT-4.1.5-40743-7d7add5.7z'; archiveSha256 = '5cc04274c88115730fe982fd12c7525d57e5fc64b6b7271ab3929383e3ac4432'; eftBuild = '0.16.9.5.40743'; sptBuild = '4.1.5-RELEASE+7d7add5.20260905'; sourceCommit = '7d7add5' }
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
