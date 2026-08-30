param(
    [Parameter(Mandatory = $true)]
    [string]$ProfilePath,

    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$frozenTraderId = 'd5c27bb3169f8dfbc13f6b69'
$moduleRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$identityLedgerPath = Join-Path $moduleRoot 'manifests\persistent-identities.json'

if (-not (Test-Path $ProfilePath -PathType Leaf)) { throw "Profile JSON not found: $ProfilePath" }
if (-not (Test-Path $identityLedgerPath -PathType Leaf)) { throw "Admiral persistent identity ledger not found: $identityLedgerPath" }

$ledger = Get-Content $identityLedgerPath -Raw -Encoding UTF8 | ConvertFrom-Json -Depth 100
if ($ledger.product -ne 'Admiral Trader' -or $ledger.targetSptVersion -ne '4.1.3') { throw 'Persistent identity ledger product/target drift.' }
if ($ledger.policy.preserveDistributedIds -ne $true -or $ledger.policy.reuseRetiredIds -ne $false -or $ledger.policy.silentRemovalAllowed -ne $false -or $ledger.policy.retirementRequiresRecoveryCoverage -ne $true) {
    throw 'Persistent identity ledger policy is not fail-closed.'
}

$currentTraderIds = @($ledger.current.traderIds | ForEach-Object { [string]$_ } | Sort-Object -Unique)
$currentQuestIds = @($ledger.current.questIds | ForEach-Object { [string]$_ } | Sort-Object -Unique)
$currentOfferIds = @($ledger.current.offerIds | ForEach-Object { [string]$_ } | Sort-Object -Unique)
$retiredTraderIds = @($ledger.retired.traderIds | ForEach-Object { [string]$_ } | Sort-Object -Unique)
$retiredQuestIds = @($ledger.retired.questIds | ForEach-Object { [string]$_ } | Sort-Object -Unique)
$retiredOfferIds = @($ledger.retired.offerIds | ForEach-Object { [string]$_ } | Sort-Object -Unique)

if ($currentTraderIds.Count -ne 1 -or $currentTraderIds[0] -ne $frozenTraderId) { throw "Expected exactly frozen Admiral trader id $frozenTraderId in current identity ledger." }
if ($currentQuestIds.Count -ne 31) { throw "Expected exactly 31 current Admiral quest IDs in persistent identity ledger, found $($currentQuestIds.Count)" }
if ($currentOfferIds.Count -ne 11) { throw "Expected exactly 11 current Admiral offer IDs in persistent identity ledger, found $($currentOfferIds.Count)" }

$traderIds = @($currentTraderIds + $retiredTraderIds | Sort-Object -Unique)
$questIds = @($currentQuestIds + $retiredQuestIds | Sort-Object -Unique)
$offerIds = @($currentOfferIds + $retiredOfferIds | Sort-Object -Unique)
foreach ($id in @($traderIds + $questIds + $offerIds)) {
    if ($id -notmatch '^[0-9a-f]{24}$') { throw "Malformed Admiral persistent identity: $id" }
}

$traderSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($id in $traderIds) { [void]$traderSet.Add($id) }
$questSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($id in $questIds) { [void]$questSet.Add($id) }

function Get-OwnedStateSummary {
    param([Parameter(Mandatory = $true)]$Profile)

    if ($null -eq $Profile.characters -or $null -eq $Profile.characters.pmc) { throw 'Profile does not contain characters.pmc' }
    $localPmc = $Profile.characters.pmc
    $ownedTraderInfo = 0
    $ownedDialogues = 0
    $ownedPurchases = 0
    foreach ($traderId in $traderIds) {
        if ($null -ne $localPmc.TradersInfo -and $null -ne $localPmc.TradersInfo.PSObject.Properties[$traderId]) { $ownedTraderInfo++ }
        if ($null -ne $Profile.dialogues -and $null -ne $Profile.dialogues.PSObject.Properties[$traderId]) { $ownedDialogues++ }
        if ($null -ne $Profile.traderPurchases -and $null -ne $Profile.traderPurchases.PSObject.Properties[$traderId]) { $ownedPurchases++ }
    }

    $questStatuses = 0
    if ($null -ne $localPmc.Quests) {
        $questStatuses = @($localPmc.Quests | Where-Object { $questSet.Contains([string]$_.qid) }).Count
    }

    $taskCounters = 0
    if ($null -ne $localPmc.TaskConditionCounters) {
        foreach ($property in @($localPmc.TaskConditionCounters.PSObject.Properties)) {
            if ($null -ne $property.Value -and $questSet.Contains([string]$property.Value.sourceId)) { $taskCounters++ }
        }
    }

    return [ordered]@{
        traderInfo = $ownedTraderInfo
        questStatuses = $questStatuses
        taskCounters = $taskCounters
        dialogue = $ownedDialogues
        traderPurchases = $ownedPurchases
    }
}

$resolvedProfile = (Resolve-Path $ProfilePath).Path
$raw = Get-Content $resolvedProfile -Raw -Encoding UTF8
$profile = $raw | ConvertFrom-Json -Depth 100
if ($null -eq $profile.characters -or $null -eq $profile.characters.pmc) { throw 'Profile does not contain characters.pmc' }
$pmc = $profile.characters.pmc

$ownedTraderInfo = [System.Collections.Generic.List[string]]::new()
$ownedDialogues = [System.Collections.Generic.List[string]]::new()
$ownedPurchases = [System.Collections.Generic.List[string]]::new()
foreach ($traderId in $traderIds) {
    if ($null -ne $pmc.TradersInfo -and $null -ne $pmc.TradersInfo.PSObject.Properties[$traderId]) { $ownedTraderInfo.Add($traderId) }
    if ($null -ne $profile.dialogues -and $null -ne $profile.dialogues.PSObject.Properties[$traderId]) { $ownedDialogues.Add($traderId) }
    if ($null -ne $profile.traderPurchases -and $null -ne $profile.traderPurchases.PSObject.Properties[$traderId]) { $ownedPurchases.Add($traderId) }
}

$ownedBefore = Get-OwnedStateSummary -Profile $profile
$summary = [ordered]@{
    profile = $resolvedProfile
    currentTraderIds = $currentTraderIds.Count
    retiredTraderIds = $retiredTraderIds.Count
    currentQuestIds = $currentQuestIds.Count
    retiredQuestIds = $retiredQuestIds.Count
    currentOfferIds = $currentOfferIds.Count
    retiredOfferIds = $retiredOfferIds.Count
    traderInfo = $ownedBefore.traderInfo
    questStatuses = $ownedBefore.questStatuses
    taskCounters = $ownedBefore.taskCounters
    dialogue = $ownedBefore.dialogue
    traderPurchases = $ownedBefore.traderPurchases
}

Write-Host 'Admiral Trader profile recovery preview:'
$summary.GetEnumerator() | ForEach-Object { Write-Host ("  {0}: {1}" -f $_.Key, $_.Value) }

if (-not $Apply) {
    Write-Host 'Dry run only. Re-run with -Apply to create a verified backup and remove only current/retired Admiral-owned profile state.'
    exit 0
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupPath = "$resolvedProfile.admiral-trader-backup-$timestamp"
Copy-Item $resolvedProfile $backupPath -Force
$sourceHash = (Get-FileHash $resolvedProfile -Algorithm SHA256).Hash
$backupHash = (Get-FileHash $backupPath -Algorithm SHA256).Hash
if ($sourceHash -ne $backupHash) {
    Remove-Item $backupPath -Force -ErrorAction SilentlyContinue
    throw 'Backup verification failed; profile was not modified.'
}

$tempPath = "$resolvedProfile.admiral-trader-write-$timestamp.tmp"
try {
    foreach ($traderId in $ownedTraderInfo) { $pmc.TradersInfo.PSObject.Properties.Remove($traderId) }
    if ($null -ne $pmc.Quests) { $pmc.Quests = @($pmc.Quests | Where-Object { -not $questSet.Contains([string]$_.qid) }) }
    if ($null -ne $pmc.TaskConditionCounters) {
        foreach ($property in @($pmc.TaskConditionCounters.PSObject.Properties)) {
            if ($null -ne $property.Value -and $questSet.Contains([string]$property.Value.sourceId)) {
                $pmc.TaskConditionCounters.PSObject.Properties.Remove($property.Name)
            }
        }
    }
    foreach ($traderId in $ownedDialogues) { $profile.dialogues.PSObject.Properties.Remove($traderId) }
    foreach ($traderId in $ownedPurchases) { $profile.traderPurchases.PSObject.Properties.Remove($traderId) }

    $profile | ConvertTo-Json -Depth 100 -Compress | Set-Content $tempPath -Encoding UTF8
    $roundTrip = Get-Content $tempPath -Raw -Encoding UTF8 | ConvertFrom-Json -Depth 100
    if ($null -eq $roundTrip.characters -or $null -eq $roundTrip.characters.pmc) { throw 'Round-trip profile validation failed.' }
    $tempOwned = Get-OwnedStateSummary -Profile $roundTrip
    foreach ($key in @('traderInfo', 'questStatuses', 'taskCounters', 'dialogue', 'traderPurchases')) {
        if ([int]$tempOwned[$key] -ne 0) { throw "Recovery postcondition failed before commit: Admiral-owned $key remains ($($tempOwned[$key]))." }
    }

    Move-Item $tempPath $resolvedProfile -Force

    $committed = Get-Content $resolvedProfile -Raw -Encoding UTF8 | ConvertFrom-Json -Depth 100
    $committedOwned = Get-OwnedStateSummary -Profile $committed
    foreach ($key in @('traderInfo', 'questStatuses', 'taskCounters', 'dialogue', 'traderPurchases')) {
        if ([int]$committedOwned[$key] -ne 0) { throw "Recovery postcondition failed after commit: Admiral-owned $key remains ($($committedOwned[$key]))." }
    }
}
catch {
    if (Test-Path $tempPath) { Remove-Item $tempPath -Force -ErrorAction SilentlyContinue }
    if (Test-Path $backupPath) {
        Copy-Item $backupPath $resolvedProfile -Force
        $restoredHash = (Get-FileHash $resolvedProfile -Algorithm SHA256).Hash
        if ($restoredHash -ne $backupHash) { throw "Recovery failed and automatic backup restore hash verification also failed. Backup retained at: $backupPath" }
    }
    throw
}

Write-Host "Profile recovery applied. Verified backup retained at: $backupPath"
Write-Host 'Postconditions verified: no current/retired Admiral TraderInfo, quest status, task counter, dialogue or trader-purchase state remains.'
Write-Host 'On next SPT trader access the current Admiral TraderInfo is recreated from the profile template with standing 0; current quests return to their normal unaccepted/availability lifecycle.'
