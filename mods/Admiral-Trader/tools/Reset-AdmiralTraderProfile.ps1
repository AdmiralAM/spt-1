param(
    [Parameter(Mandatory = $true)]
    [string]$ProfilePath,

    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$traderId = 'd5c27bb3169f8dfbc13f6b69'
$moduleRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$questRoot = Join-Path $moduleRoot 'db\quests'

if (-not (Test-Path $ProfilePath -PathType Leaf)) { throw "Profile JSON not found: $ProfilePath" }
if (-not (Test-Path $questRoot -PathType Container)) { throw "Admiral quest directory not found: $questRoot" }

$resolvedProfile = (Resolve-Path $ProfilePath).Path
$questIds = @(
    Get-ChildItem $questRoot -Filter '*.json' -File |
        ForEach-Object { (Get-Content $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json)._id } |
        Where-Object { $_ -match '^[0-9a-f]{24}$' } |
        Sort-Object -Unique
)
if ($questIds.Count -ne 31) { throw "Expected exactly 31 canonical Admiral quest IDs, found $($questIds.Count)" }
$questSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($qid in $questIds) { [void]$questSet.Add([string]$qid) }

$raw = Get-Content $resolvedProfile -Raw -Encoding UTF8
$profile = $raw | ConvertFrom-Json -Depth 100
if ($null -eq $profile.characters -or $null -eq $profile.characters.pmc) { throw 'Profile does not contain characters.pmc' }
$pmc = $profile.characters.pmc

$summary = [ordered]@{
    profile = $resolvedProfile
    traderInfo = 0
    questStatuses = 0
    taskCounters = 0
    dialogue = 0
    traderPurchases = 0
}

if ($null -ne $pmc.TradersInfo -and $null -ne $pmc.TradersInfo.PSObject.Properties[$traderId]) {
    $summary.traderInfo = 1
}
if ($null -ne $pmc.Quests) {
    $summary.questStatuses = @($pmc.Quests | Where-Object { $questSet.Contains([string]$_.qid) }).Count
}
if ($null -ne $pmc.TaskConditionCounters) {
    foreach ($property in @($pmc.TaskConditionCounters.PSObject.Properties)) {
        if ($null -ne $property.Value -and $questSet.Contains([string]$property.Value.sourceId)) { $summary.taskCounters++ }
    }
}
if ($null -ne $profile.dialogues -and $null -ne $profile.dialogues.PSObject.Properties[$traderId]) {
    $summary.dialogue = 1
}
if ($null -ne $profile.traderPurchases -and $null -ne $profile.traderPurchases.PSObject.Properties[$traderId]) {
    $summary.traderPurchases = 1
}

Write-Host 'Admiral Trader profile recovery preview:'
$summary.GetEnumerator() | ForEach-Object { Write-Host ("  {0}: {1}" -f $_.Key, $_.Value) }

if (-not $Apply) {
    Write-Host 'Dry run only. Re-run with -Apply to create a verified backup and remove only Admiral-owned profile state.'
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

try {
    if ($summary.traderInfo -eq 1) { $pmc.TradersInfo.PSObject.Properties.Remove($traderId) }
    if ($null -ne $pmc.Quests) { $pmc.Quests = @($pmc.Quests | Where-Object { -not $questSet.Contains([string]$_.qid) }) }
    if ($null -ne $pmc.TaskConditionCounters) {
        foreach ($property in @($pmc.TaskConditionCounters.PSObject.Properties)) {
            if ($null -ne $property.Value -and $questSet.Contains([string]$property.Value.sourceId)) {
                $pmc.TaskConditionCounters.PSObject.Properties.Remove($property.Name)
            }
        }
    }
    if ($summary.dialogue -eq 1) { $profile.dialogues.PSObject.Properties.Remove($traderId) }
    if ($summary.traderPurchases -eq 1) { $profile.traderPurchases.PSObject.Properties.Remove($traderId) }

    $tempPath = "$resolvedProfile.admiral-trader-write-$timestamp.tmp"
    $profile | ConvertTo-Json -Depth 100 -Compress | Set-Content $tempPath -Encoding UTF8
    $roundTrip = Get-Content $tempPath -Raw -Encoding UTF8 | ConvertFrom-Json -Depth 100
    if ($null -eq $roundTrip.characters -or $null -eq $roundTrip.characters.pmc) { throw 'Round-trip profile validation failed.' }
    Move-Item $tempPath $resolvedProfile -Force
}
catch {
    if (Test-Path $backupPath) { Copy-Item $backupPath $resolvedProfile -Force }
    throw
}

Write-Host "Profile recovery applied. Verified backup retained at: $backupPath"
Write-Host 'On next SPT startup Admiral TraderInfo will be recreated from the profile template with standing 0; Admiral quests will return to their normal unaccepted/availability lifecycle.'
