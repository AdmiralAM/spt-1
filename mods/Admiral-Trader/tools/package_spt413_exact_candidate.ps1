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
$recoverySource = Join-Path $PSScriptRoot 'Reset-AdmiralTraderProfile.ps1'

if (-not (Test-Path $builder)) { throw "Exact-runtime builder is missing: $builder" }
if (-not (Test-Path $recoverySource)) { throw "Profile recovery tool is missing: $recoverySource" }

$expected = $ExpectedHeadSha.Trim().ToLowerInvariant()
if ($expected -notmatch '^[0-9a-f]{40}$') { throw 'ExpectedHeadSha must be the full 40-character hexadecimal Git SHA.' }

& $builder -SptRoot $SptRoot -ExpectedHeadSha $expected
if ($LASTEXITCODE -ne 0) { throw "Exact-runtime candidate builder failed with exit code $LASTEXITCODE" }

$sourceHead = (& git -C $repoRoot rev-parse HEAD).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $sourceHead -notmatch '^[0-9a-f]{40}$') { throw 'Unable to resolve exact source HEAD after candidate build.' }
if ($sourceHead -ne $expected) { throw "Post-build source HEAD mismatch: expected $expected, found $sourceHead" }

$stageRoot = Join-Path $repoRoot 'build\admiral-trader-test-candidate'
$stageMod = Join-Path $stageRoot 'SPT_Runtime\user\mods\Admiral-Trader'
$provenancePath = Join-Path $stageMod 'candidate-provenance.json'
$manifestPath = Join-Path $stageMod 'manifests\runtime-manifest.json'
$identityPath = Join-Path $stageMod 'manifests\identity-assets.json'
$persistentIdentityPath = Join-Path $stageMod 'manifests\persistent-identities.json'
$basePath = Join-Path $stageMod 'db\base.json'
$assortPath = Join-Path $stageMod 'db\assort.json'
$questAssortPath = Join-Path $stageMod 'db\questassort.json'
$serverDllPath = Join-Path $stageMod 'Admiral Trader Server.dll'
$recoveryPath = Join-Path $stageMod 'tools\Reset-AdmiralTraderProfile.ps1'
New-Item (Split-Path $recoveryPath -Parent) -ItemType Directory -Force | Out-Null
Copy-Item $recoverySource $recoveryPath -Force

foreach ($required in @($provenancePath, $manifestPath, $identityPath, $persistentIdentityPath, $basePath, $assortPath, $questAssortPath, $serverDllPath, $recoveryPath)) {
    if (-not (Test-Path $required)) { throw "Exact-runtime staging is incomplete: missing $required" }
}

$provenance = Get-Content $provenancePath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($provenance.sourceHeadSha -ne $sourceHead) { throw "Candidate provenance source HEAD mismatch: $($provenance.sourceHeadSha) != $sourceHead" }
if ($provenance.compileMode -ne 'exact-installed-runtime') { throw "Refusing to package non-exact candidate compileMode=$($provenance.compileMode)" }
if ($provenance.publicationMode -ne 'test-candidate') { throw "Refusing to package publicationMode=$($provenance.publicationMode)" }
if ($provenance.physicalRuntimeEvidenceEligible -ne $true) { throw 'Refusing to package candidate that is not physical-runtime-evidence eligible.' }
if ([string]::IsNullOrWhiteSpace([string]$provenance.runtimeCoreSha256) -or $provenance.runtimeCoreSha256 -notmatch '^[0-9a-f]{64}$') { throw 'Exact-runtime provenance must contain a 64-hex runtimeCoreSha256.' }
if ($null -ne $provenance.PSObject.Properties['publishedApiCoreSha256']) { throw 'Exact-runtime candidate must not contain publishedApiCoreSha256.' }
if ([string]::IsNullOrWhiteSpace([string]$provenance.serverDllSha256) -or $provenance.serverDllSha256 -notmatch '^[0-9a-f]{64}$') { throw 'Exact-runtime provenance must contain a 64-hex serverDllSha256.' }
if ([string]::IsNullOrWhiteSpace([string]$provenance.officialPortraitSha256) -or $provenance.officialPortraitSha256 -notmatch '^[0-9a-f]{64}$') { throw 'Exact-runtime provenance must contain a 64-hex officialPortraitSha256.' }
if ([string]::IsNullOrWhiteSpace([string]$provenance.officialPortraitGitBlobSha1) -or $provenance.officialPortraitGitBlobSha1 -notmatch '^[0-9a-f]{40}$') { throw 'Exact-runtime provenance must contain a 40-hex officialPortraitGitBlobSha1.' }

$actualServerDllSha256 = (Get-FileHash $serverDllPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualServerDllSha256 -ne $provenance.serverDllSha256) { throw "Staged Admiral server DLL hash drift: provenance=$($provenance.serverDllSha256) actual=$actualServerDllSha256" }

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$identity = Get-Content $identityPath -Raw -Encoding UTF8 | ConvertFrom-Json
$persistentIdentity = Get-Content $persistentIdentityPath -Raw -Encoding UTF8 | ConvertFrom-Json
$base = Get-Content $basePath -Raw -Encoding UTF8 | ConvertFrom-Json
$assort = Get-Content $assortPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.registrationEnabled -ne $true -or $manifest.targetSptVersion -ne '4.1.3' -or $manifest.publicationMode -ne 'test-candidate') { throw 'Staged runtime manifest is not an enabled exact SPT 4.1.3 test candidate.' }

if ($persistentIdentity.product -ne 'Admiral Trader' -or $persistentIdentity.targetSptVersion -ne '4.1.3') { throw 'Persistent identity ledger product/target drift in staged candidate.' }
if ($persistentIdentity.policy.preserveDistributedIds -ne $true -or $persistentIdentity.policy.reuseRetiredIds -ne $false -or $persistentIdentity.policy.silentRemovalAllowed -ne $false -or $persistentIdentity.policy.retirementRequiresRecoveryCoverage -ne $true) { throw 'Persistent identity ledger policy is not fail-closed in staged candidate.' }
$currentTraderIds = @($persistentIdentity.current.traderIds | ForEach-Object { [string]$_ })
$currentQuestIds = @($persistentIdentity.current.questIds | ForEach-Object { [string]$_ })
$currentOfferIds = @($persistentIdentity.current.offerIds | ForEach-Object { [string]$_ })
if ($currentTraderIds.Count -ne 1 -or $currentTraderIds[0] -ne [string]$identity.traderId -or $currentTraderIds[0] -ne [string]$base._id) { throw 'Persistent current trader identity drift in staged candidate.' }
if ($currentQuestIds.Count -ne 31 -or @($currentQuestIds | Sort-Object -Unique).Count -ne 31) { throw "Persistent current quest identity count drift: $($currentQuestIds.Count)" }
if ($currentOfferIds.Count -ne 11 -or @($currentOfferIds | Sort-Object -Unique).Count -ne 11) { throw "Persistent current offer identity count drift: $($currentOfferIds.Count)" }
$rootOfferIds = @($assort.items | Where-Object { $_.parentId -eq 'hideout' } | ForEach-Object { [string]$_._id } | Sort-Object -Unique)
if ($rootOfferIds.Count -ne 11 -or (Compare-Object ($currentOfferIds | Sort-Object) $rootOfferIds)) { throw 'Persistent current offer identities do not match staged assort roots.' }
$questFiles = @(Get-ChildItem (Join-Path $stageMod 'db\quests') -Filter '*.json' -File)
$runtimeQuestIds = @($questFiles | ForEach-Object { [string](Get-Content $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json)._id } | Sort-Object -Unique)
if ($runtimeQuestIds.Count -ne 31 -or (Compare-Object ($currentQuestIds | Sort-Object) $runtimeQuestIds)) { throw 'Persistent current quest identities do not match staged runtime quests.' }
foreach ($domain in @('traderIds','questIds','offerIds')) {
    $currentIds = @($persistentIdentity.current.$domain | ForEach-Object { [string]$_ })
    $retiredIds = @($persistentIdentity.retired.$domain | ForEach-Object { [string]$_ })
    if (@($currentIds | Sort-Object -Unique).Count -ne $currentIds.Count -or @($retiredIds | Sort-Object -Unique).Count -ne $retiredIds.Count) { throw "Persistent $domain contains duplicate identities." }
    if (@(Compare-Object $currentIds $retiredIds -IncludeEqual -ExcludeDifferent).Count -ne 0) { throw "Persistent $domain reuses a retired identity." }
}

$portraitRelative = [string]$identity.portrait.runtimeAsset
$portraitRoute = [string]$identity.portrait.runtimeRoute
$portraitPath = Join-Path $stageMod ($portraitRelative -replace '/', '\')
if (-not (Test-Path $portraitPath)) { throw "Exact-runtime staging is missing official portrait: $portraitRelative" }
if ($base.avatar -ne $portraitRoute) { throw "Portrait route drift: base=$($base.avatar) identity=$portraitRoute" }
if ($portraitRoute -ne "/files/trader/avatar/$($identity.traderId).jpg") { throw "Official portrait route is not bound to trader id: $portraitRoute" }
if ([string]$identity.portrait.runtimeGitBlobSha1 -ne [string]$provenance.officialPortraitGitBlobSha1) { throw 'Staged portrait Git blob identity drift between identity and provenance.' }
$portraitSha256 = (Get-FileHash $portraitPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($portraitSha256 -ne [string]$provenance.officialPortraitSha256) { throw "Staged official portrait provenance drift: provenance=$($provenance.officialPortraitSha256) actual=$portraitSha256" }
if ([string]$provenance.officialPortraitRoute -ne $portraitRoute) { throw "Staged official portrait route provenance drift: provenance=$($provenance.officialPortraitRoute) identity=$portraitRoute" }
$portraitBytes = [System.IO.File]::ReadAllBytes($portraitPath)
if ($portraitBytes.Length -lt 4 -or $portraitBytes[0] -ne 0xFF -or $portraitBytes[1] -ne 0xD8 -or $portraitBytes[$portraitBytes.Length - 2] -ne 0xFF -or $portraitBytes[$portraitBytes.Length - 1] -ne 0xD9) { throw 'Staged official portrait is not a complete JPEG stream.' }

$questAssort = Get-Content $questAssortPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$requiredQuestAssortKeys = @('started', 'success', 'fail')
if ($questAssort.Keys.Count -ne 3 -or @($requiredQuestAssortKeys | Where-Object { -not $questAssort.ContainsKey($_) }).Count -ne 0) { throw "Staged questassort must contain exactly native lowercase started/success/fail; found: $($questAssort.Keys -join ', ')" }
if ($questAssort['success'].Count -ne 7) { throw "Staged questassort must retain exactly seven success unlock mappings; found $($questAssort['success'].Count)" }

$artifactName = "Admiral-Trader-SPT413-$sourceHead.zip"
$artifactPath = Join-Path (Join-Path $repoRoot 'build') $artifactName
$shaPath = "$artifactPath.sha256"
if (Test-Path $artifactPath) { Remove-Item $artifactPath -Force }
if (Test-Path $shaPath) { Remove-Item $shaPath -Force }
Compress-Archive -Path (Join-Path $stageRoot 'SPT_Runtime') -DestinationPath $artifactPath -CompressionLevel Optimal
if (-not (Test-Path $artifactPath)) { throw "Exact-runtime candidate archive was not created: $artifactPath" }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($artifactPath)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    $requiredEntries = @(
        'SPT_Runtime/user/mods/Admiral-Trader/candidate-provenance.json',
        'SPT_Runtime/user/mods/Admiral-Trader/Admiral Trader Server.dll',
        'SPT_Runtime/user/mods/Admiral-Trader/manifests/runtime-manifest.json',
        'SPT_Runtime/user/mods/Admiral-Trader/manifests/identity-assets.json',
        'SPT_Runtime/user/mods/Admiral-Trader/manifests/persistent-identities.json',
        'SPT_Runtime/user/mods/Admiral-Trader/db/base.json',
        'SPT_Runtime/user/mods/Admiral-Trader/db/assort.json',
        'SPT_Runtime/user/mods/Admiral-Trader/db/questassort.json',
        'SPT_Runtime/user/mods/Admiral-Trader/tools/Reset-AdmiralTraderProfile.ps1',
        "SPT_Runtime/user/mods/Admiral-Trader/$portraitRelative"
    )
    foreach ($entry in $requiredEntries) { if ($entries -notcontains $entry) { throw "Exact-runtime archive layout is invalid; missing $entry" } }
    if (@($entries | Where-Object { $_ -match '(^|/)(bin|obj)/' -or $_ -match '\.(pdb|log|zip)$' }).Count -ne 0) { throw 'Exact-runtime archive contains forbidden build/debug junk.' }
}
finally { $archive.Dispose() }

$artifactSha256 = (Get-FileHash $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$artifactSha256  $artifactName" | Set-Content $shaPath -Encoding ascii

if ($Install) {
    $root = (Resolve-Path $SptRoot).Path
    $runtimeRoot = if (Test-Path (Join-Path $root 'SPTarkov.Server.Core.dll')) { $root } elseif (Test-Path (Join-Path $root 'SPT_Runtime\SPTarkov.Server.Core.dll')) { Join-Path $root 'SPT_Runtime' } else { throw 'Cannot resolve SPT runtime root for final validated install.' }
    $modsRoot = Join-Path $runtimeRoot 'user\mods'
    $destination = Join-Path $modsRoot 'Admiral-Trader'
    $incoming = Join-Path $modsRoot '.Admiral-Trader.incoming'
    $backup = Join-Path $modsRoot '.Admiral-Trader.rollback'
    New-Item $modsRoot -ItemType Directory -Force | Out-Null

    foreach ($scratch in @($incoming, $backup)) { if (Test-Path $scratch) { Remove-Item $scratch -Recurse -Force } }
    Copy-Item $stageMod $incoming -Recurse
    foreach ($requiredRelative in @('candidate-provenance.json','Admiral Trader Server.dll','manifests\runtime-manifest.json','manifests\identity-assets.json','manifests\persistent-identities.json','db\base.json','db\assort.json','db\questassort.json','tools\Reset-AdmiralTraderProfile.ps1',($portraitRelative -replace '/', '\'))) {
        if (-not (Test-Path (Join-Path $incoming $requiredRelative))) {
            Remove-Item $incoming -Recurse -Force -ErrorAction SilentlyContinue
            throw "Prepared install tree is incomplete: missing $requiredRelative"
        }
    }
    $incomingDllSha256 = (Get-FileHash (Join-Path $incoming 'Admiral Trader Server.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($incomingDllSha256 -ne $provenance.serverDllSha256) {
        Remove-Item $incoming -Recurse -Force -ErrorAction SilentlyContinue
        throw "Prepared install DLL hash drift: provenance=$($provenance.serverDllSha256) actual=$incomingDllSha256"
    }
    $incomingPortraitSha256 = (Get-FileHash (Join-Path $incoming ($portraitRelative -replace '/', '\')) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($incomingPortraitSha256 -ne $portraitSha256) {
        Remove-Item $incoming -Recurse -Force -ErrorAction SilentlyContinue
        throw "Prepared install portrait hash drift: staged=$portraitSha256 incoming=$incomingPortraitSha256"
    }

    $hadExistingInstall = Test-Path $destination
    try {
        if ($hadExistingInstall) { Move-Item $destination $backup }
        Move-Item $incoming $destination
    }
    catch {
        if (Test-Path $destination) { Remove-Item $destination -Recurse -Force -ErrorAction SilentlyContinue }
        if ($hadExistingInstall -and (Test-Path $backup)) { Move-Item $backup $destination -ErrorAction SilentlyContinue }
        if (Test-Path $incoming) { Remove-Item $incoming -Recurse -Force -ErrorAction SilentlyContinue }
        throw
    }
    if (Test-Path $backup) { Remove-Item $backup -Recurse -Force }
    if (Test-Path $incoming) { Remove-Item $incoming -Recurse -Force }
    Write-Host "Installed fully validated exact-runtime test candidate to: $destination"
}

Write-Host "Exact-runtime candidate package ready: $artifactPath"
Write-Host "Candidate SHA-256: $artifactSha256"
Write-Host "Source HEAD: $sourceHead"
Write-Host "Installed runtime Server.Core SHA-256: $($provenance.runtimeCoreSha256)"
Write-Host "Admiral server DLL SHA-256: $($provenance.serverDllSha256)"
Write-Host "Official portrait SHA-256: $portraitSha256"
Write-Host 'Install tree inside archive: SPT_Runtime/user/mods/Admiral-Trader'
