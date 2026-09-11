param(
    [Parameter(Mandatory = $true)][string]$SptRoot,
    [Parameter(Mandatory = $true)][string]$SourceHeadSha,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$traderRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $traderRoot '../..')).Path
$runtimeRoot = if (Test-Path (Join-Path $SptRoot 'SPTarkov.Server.Core.dll')) {
    (Resolve-Path $SptRoot).Path
} elseif (Test-Path (Join-Path $SptRoot 'SPT_Runtime/SPTarkov.Server.Core.dll')) {
    (Resolve-Path (Join-Path $SptRoot 'SPT_Runtime')).Path
} else { throw "SPT 4.1.5 runtime root was not found below $SptRoot" }

$sourceHead = (& git -C $repoRoot rev-parse HEAD).Trim().ToLowerInvariant()
if ($sourceHead -ne $SourceHeadSha.ToLowerInvariant()) { throw "Trader source head mismatch: $sourceHead != $SourceHeadSha" }

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

$runtimeManifestPath = Join-Path $traderRoot 'manifests/runtime-manifest.json'
$runtimeManifest = Get-Content $runtimeManifestPath -Raw | ConvertFrom-Json
if ($runtimeManifest.schemaVersion -ne 2 -or $runtimeManifest.version -ne '0.1.0+milestones' -or $runtimeManifest.sptCompatibility -ne '~4.1.0') { throw 'Trader release metadata drift.' }
if ($runtimeManifest.targetSptVersion -ne '4.1.5') { throw "Trader runtime target drift: $($runtimeManifest.targetSptVersion)" }
if ($runtimeManifest.publishedApiCompileBaseline -ne '4.1.5') { throw 'Trader published API baseline drift.' }
if ($runtimeManifest.registrationEnabled -ne $false) { throw 'Source runtime registration must remain fail-closed; only staged RC may enable it.' }

$portrait = Join-Path $traderRoot 'assets/d5c27bb3169f8dfbc13f6b69.jpg'
if (-not (Test-Path $portrait -PathType Leaf)) { throw 'Approved Admiral portrait is missing from active source.' }
$portraitBlob = (& git -C $repoRoot hash-object $portrait).Trim().ToLowerInvariant()
if ($portraitBlob -ne '63e158fbd96b595a609560dfef452451b4783144') {
    throw "Approved Admiral portrait blob drift: $portraitBlob"
}

python (Join-Path $traderRoot 'tools/validate_runtime_assort.py')
if ($LASTEXITCODE -ne 0) { throw 'Trader 4 Baseline + 3 Relationship + 8 Milestone runtime assort contract failed.' }

$itemsPath = Join-Path $runtimeRoot 'SPT_Data/database/templates/items.json'
if (-not (Test-Path $itemsPath -PathType Leaf)) { throw "Exact SPT item database is missing: $itemsPath" }
$itemDb = Get-Content $itemsPath -Raw | ConvertFrom-Json -AsHashtable
$assort = Get-Content (Join-Path $traderRoot 'db/assort.json') -Raw | ConvertFrom-Json
$baseline = Get-Content (Join-Path $traderRoot 'manifests/baseline-stock.json') -Raw | ConvertFrom-Json
$relationship = Get-Content (Join-Path $traderRoot 'manifests/relationship-stock.json') -Raw | ConvertFrom-Json
$storefrontCore = Get-Content (Join-Path $traderRoot 'manifests/storefront-core-expansion.json') -Raw | ConvertFrom-Json
$questAssort = Get-Content (Join-Path $traderRoot 'db/questassort.json') -Raw | ConvertFrom-Json
$rootOffers = @($assort.items | Where-Object parentId -eq 'hideout')
if ($rootOffers.Count -ne 37) { throw "Expected 37 active-head root offers, got $($rootOffers.Count)" }
$missingTpls = @($rootOffers | ForEach-Object { [string]$_."_tpl" } | Where-Object { -not $itemDb.ContainsKey($_) } | Sort-Object -Unique)
if ($missingTpls.Count) { throw "Active-head assort contains TPLs missing from exact SPT 4.1.5 DB: $($missingTpls -join ', ')" }
$baselineIds = @($baseline.offers | ForEach-Object { [string]$_.offerId })
$relationshipIds = @($relationship.offers | ForEach-Object { [string]$_.offerId })
$storefrontCoreIds = @($storefrontCore.offers | ForEach-Object { [string]$_.offerId })
$milestoneIds = @($questAssort.success.PSObject.Properties | ForEach-Object { [string]$_.Name })
if ($baselineIds.Count -ne 4 -or ($baselineIds | Sort-Object -Unique).Count -ne 4) { throw 'Baseline authority must contain four unique offers.' }
if ($milestoneIds.Count -ne 8 -or ($milestoneIds | Sort-Object -Unique).Count -ne 8) { throw 'Milestone questassort must contain eight unique offers.' }
if ($relationship.materialization.enabled -ne $true -or $relationshipIds.Count -ne 3 -or ($relationshipIds | Sort-Object -Unique).Count -ne 3) { throw 'M5 Relationship authority must contain three unique materialized offers.' }
if ($storefrontCoreIds.Count -ne 22 -or ($storefrontCoreIds | Sort-Object -Unique).Count -ne 22) { throw 'Stabilized storefront authority must contain 22 unique core offers.' }
if (@($baselineIds | Where-Object { $_ -in $milestoneIds }).Count) { throw 'Baseline offers must not be quest-gated Milestone offers.' }
if (@($relationshipIds | Where-Object { $_ -in $milestoneIds -or $_ -in $baselineIds }).Count) { throw 'Relationship offers must remain distinct from Baseline and Milestone offers.' }
$rootIds = @($rootOffers | ForEach-Object { [string]$_."_id" })
$unclassified = @($rootIds | Where-Object { $_ -notin $baselineIds -and $_ -notin $relationshipIds -and $_ -notin $milestoneIds -and $_ -notin $storefrontCoreIds })
if ($unclassified.Count) { throw "Unclassified offers materialized unexpectedly: $($unclassified -join ', ')" }

$questFiles = @(Get-ChildItem (Join-Path $traderRoot 'db/quests') -Filter '*.json' -File)
if ($questFiles.Count -ne 43) { throw "Trader quest count drift: $($questFiles.Count)" }

$project = Join-Path $traderRoot 'server/AdmiralTrader.Server.csproj'
dotnet build $project -c Release --nologo "-p:SptRuntimeLibDir=$runtimeRoot"
if ($LASTEXITCODE -ne 0) { throw 'Active-head Trader exact-runtime build failed.' }

$dll = Join-Path $traderRoot 'server/bin/Release/net10.0/Admiral Trader Server.dll'
if (-not (Test-Path $dll -PathType Leaf)) { throw "Compiled Trader DLL is missing: $dll" }
$dllHash = (Get-FileHash $dll -Algorithm SHA256).Hash.ToLowerInvariant()

$packageRoot = Join-Path $OutputDirectory "Admiral-Trader-0.1.0-milestones-SPT415-RC-$sourceHead"
$modTarget = Join-Path $packageRoot 'SPT_Runtime/user/mods/Admiral-Trader'
if (Test-Path $packageRoot) { Remove-Item $packageRoot -Recurse -Force }
New-Item $modTarget -ItemType Directory -Force | Out-Null
Copy-Item $dll $modTarget
foreach ($directory in 'db','manifests','assets') {
    Copy-Item (Join-Path $traderRoot $directory) (Join-Path $modTarget $directory) -Recurse
}
if (Test-Path (Join-Path $traderRoot 'README.md')) { Copy-Item (Join-Path $traderRoot 'README.md') $modTarget }
Copy-Item (Join-Path $traderRoot 'docs/INSTALL.md') (Join-Path $modTarget 'INSTALL.md')

$stagedManifestPath = Join-Path $modTarget 'manifests/runtime-manifest.json'
$stagedManifest = Get-Content $stagedManifestPath -Raw | ConvertFrom-Json
$stagedManifest.registrationEnabled = $true
$stagedManifest | Add-Member -NotePropertyName publicationMode -NotePropertyValue 'stable-release-candidate' -Force
$stagedManifest | Add-Member -NotePropertyName sourceHeadSha -NotePropertyValue $sourceHead -Force
$stagedManifest | ConvertTo-Json -Depth 20 | Set-Content $stagedManifestPath -Encoding utf8

$stagedAssort = Get-Content (Join-Path $modTarget 'db/assort.json') -Raw | ConvertFrom-Json
$stagedQuestAssort = Get-Content (Join-Path $modTarget 'db/questassort.json') -Raw | ConvertFrom-Json
if (@($stagedAssort.items | Where-Object parentId -eq 'hideout').Count -ne 37) { throw 'Staged Trader lost the 37-offer contract.' }
if (@($stagedQuestAssort.success.PSObject.Properties).Count -ne 8) { throw 'Staged Trader lost the eight Milestone gates.' }
if (@(Get-ChildItem (Join-Path $modTarget 'db/quests') -Filter '*.json' -File).Count -ne 43) { throw 'Staged Trader lost the 43-quest expanded campaign contract.' }
if (-not (Test-Path (Join-Path $modTarget 'assets/d5c27bb3169f8dfbc13f6b69.jpg') -PathType Leaf)) { throw 'Staged Trader portrait is missing.' }

$provenance = [ordered]@{
    schemaVersion = 1
    product = 'Admiral Trader'
    version = '0.1.0+milestones'
    sptCompatibility = '~4.1.0'
    releaseChannel = 'release-candidate'
    targetSptVersion = '4.1.5'
    sourceHeadSha = $sourceHead
    authority = 'PR #328 active canonical head'
    historicalReferenceOnly = '053a62ff5f1cb545f13bc89a96bba3acd319a823'
    questCount = 43
    frozenBaselineQuestCount = 31
    m3OperationQuestCount = 12
    baselineOffers = 4
    milestoneOffers = 8
    relationshipOffers = 3
    storefrontCoreOffers = 22
    totalFiniteOffers = 37
    relationshipProgression = [ordered]@{
        loyaltyLevels = 4
        standingThresholds = @(0.0, 0.1, 0.3, 0.55)
        minimumLevels = @(1, 15, 25, 35)
        profileScopedMarkerOfferId = 'ad2000000000000000000004'
        markerStockByTier = @(12, 16, 20, 24)
        markerBuyLimitByTier = @(4, 6, 8, 10)
        specialistOfferIds = $relationshipIds
    }
    selectedWeaponTemplates = 49
    weaponRotation = 'disjoint-three-stage-family-pools'
    correctedOperationRewardTotals = [ordered]@{ xp = 133000; rub = 752000; standing = 0.179 }
    specialWeaponsAmmo = [ordered]@{
        tpl = '5ede475339ee016e8c534742'
        name = '40x46mm M576 (MP-APERS) grenade'
        sampleUnits = 1
        offerId = '3500e7b76f097a98ced5d61b'
        stockPerReset = 2
        buyRestriction = 2
    }
    canonical338 = [ordered]@{ tpl = '5fc382c1016cce60e8341b20'; name = '.338 Lapua Magnum UCW' }
    traderId = 'd5c27bb3169f8dfbc13f6b69'
    portraitGitBlob = $portraitBlob
    serverDllSha256 = $dllHash
    runtimeRelease = [ordered]@{
        archive = 'SPT-4.1.5-40743-7d7add5.7z'
        archiveSha256 = '5cc04274c88115730fe982fd12c7525d57e5fc64b6b7271ab3929383e3ac4432'
        eftBuild = '0.16.9.5.40743'
        sptBuild = '4.1.5-RELEASE+7d7add5.20260905'
    }
    runtimeDllSha256 = $runtimeHashes
    exactItemDatabaseSha256 = (Get-FileHash $itemsPath -Algorithm SHA256).Hash.ToLowerInvariant()
    allAssortTplsPresentInExactItemDb = $true
    sourceRegistrationEnabled = $false
    stagedRegistrationEnabled = $true
    ownedFilesOnly = $true
}
$provenance | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $packageRoot 'admiral-trader-provenance.json') -Encoding utf8

$inventory = @(Get-ChildItem $packageRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = [IO.Path]::GetRelativePath($packageRoot, $_.FullName).Replace('\','/')
        size = $_.Length
        sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
[ordered]@{ schemaVersion = 1; sourceHeadSha = $sourceHead; files = $inventory } |
    ConvertTo-Json -Depth 6 | Set-Content (Join-Path $packageRoot 'admiral-trader-package-files.json') -Encoding utf8

& (Join-Path $traderRoot 'tools/Test-StablePackageLifecycle.ps1') -CandidateRoot $packageRoot -WorkingDirectory $OutputDirectory -ExpectedSourceHead $sourceHead

$foreign = @(Get-ChildItem $packageRoot -Recurse -File | Where-Object {
    $_.Name -in $expectedRuntime.Keys -or $_.Name -match '^(EscapeFromTarkov|SPT\.Server)'
})
if ($foreign.Count) { throw "Foreign SPT/EFT binaries leaked into package: $($foreign.FullName -join ', ')" }

New-Item $OutputDirectory -ItemType Directory -Force | Out-Null
$zip = "$packageRoot.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zip -CompressionLevel Optimal
$zipHash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$zipHash  $([IO.Path]::GetFileName($zip))" | Set-Content "$zip.sha256" -Encoding ascii
Write-Host "Admiral Trader candidate: $zip"
Write-Host "Admiral Trader candidate SHA-256: $zipHash"
