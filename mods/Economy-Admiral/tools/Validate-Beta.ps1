param(
    [Parameter(Mandatory = $false)]
    [string]$ModPath = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
function Fail([string]$Message) { Write-Host "[Economy Admiral] BETA FAIL: $Message" -ForegroundColor Red; exit 1 }
function Pass([string]$Message) { Write-Host "[Economy Admiral] BETA PASS: $Message" -ForegroundColor Green }
function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Fail "missing file: $Path" }
    if ((Get-Item -LiteralPath $Path).Length -le 0) { Fail "empty file: $Path" }
    try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { Fail "invalid JSON: $Path :: $($_.Exception.Message)" }
}

$ModPath = [System.IO.Path]::GetFullPath($ModPath)
$enforceValidator = Join-Path $ModPath 'Validate-Enforce.ps1'
if (-not (Test-Path -LiteralPath $enforceValidator -PathType Leaf)) { Fail "missing packaged Validate-Enforce.ps1" }

$pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
if ($null -eq $pwsh) { Fail "PowerShell 7 (pwsh) is required to run the packaged Enforce validator" }
& $pwsh.Source -NoProfile -File $enforceValidator -ModPath $ModPath
if ($LASTEXITCODE -ne 0) { Fail "underlying Enforce validator failed" }

$reports = Join-Path $ModPath 'reports'
$source = Read-Json (Join-Path $reports 'economy-admiral-source-pressure.json')
$health = Read-Json (Join-Path $reports 'economy-admiral-health.json')
$adapter = Read-Json (Join-Path $reports 'economy-admiral-admiral-trader-adapter.json')
$runtime = Read-Json (Join-Path $reports 'economy-admiral-runtime-evidence.json')

if ([int]$source.SchemaVersion -ne 2) { Fail "source-pressure SchemaVersion must be 2" }
if ([string]$source.EvidenceCoverage -ne 'FinalDbCore+ExplicitAdaptersWithExplicitUnknownChannels') { Fail "unexpected source-pressure coverage contract" }
if ([int]$source.SourceCount -le 0) { Fail "release candidate requires final-DB source evidence" }
# AcquisitionChannel.WorldLoot serializes as numeric enum value 6 in the runtime report.
$world = @($source.ChannelCoverage | Where-Object { [int]$_.Channel -eq 6 })
if ($world.Count -ne 1 -or [string]$world[0].State -ne 'UnknownNoMaintainedAdapter') { Fail "world-loot boundary must remain explicit UnknownNoMaintainedAdapter" }

if ([int]$health.SchemaVersion -ne 1 -or [int]$health.SourcePressureSchemaVersion -ne 2) { Fail "health/source-pressure schema link is invalid" }
if ($health.CompositeScoreSelected -ne $false) { Fail "Beta RC must not select an opaque composite health score" }
if ($health.MutationAuthorized -ne $false) { Fail "observational health report must not authorize mutation" }
if ([int]$health.ItemCount -le 0) { Fail "health report contains no observed items" }
$healthWorld = @($health.ChannelCoverage | Where-Object { [int]$_.Channel -eq 6 })
if ($healthWorld.Count -ne 1 -or [string]$healthWorld[0].State -ne 'UnknownNoMaintainedAdapter') { Fail "health report lost the explicit world-loot Unknown boundary" }

if ([int]$adapter.SchemaVersion -ne 3) { Fail "Admiral Trader adapter SchemaVersion must be 3" }
$traderInstalled = ($adapter.Installed -eq $true)
if ($traderInstalled) {
    # Admiral Trader is optional. Once present, its compatibility contract is strict and fail-closed.
    if ($adapter.ContractAvailable -ne $true) { Fail "installed Admiral Trader does not expose a supported maintained contract: $($adapter.ContractState)" }
    if ([int]$source.LoadedAdapterCount -lt 1 -or -not (@($source.LoadedAdapters) -contains 'com.admiralam.spt.admiraltrader')) { Fail "installed Admiral Trader explicit adapter is not loaded into source-pressure evidence" }
    if ([string]$adapter.ContractState -ne 'LoadedGameplayAlphaV4' -or [int]$adapter.GameplayPolicySchemaVersion -ne 4) { Fail "installed Admiral Trader must match the maintained Gameplay Alpha v4 contract" }
    if ([string]$adapter.ProductName -ne 'Admiral Trader' -or [string]$adapter.ModGuid -ne 'com.admiralam.spt.admiraltrader' -or [string]$adapter.TraderId -ne 'd5c27bb3169f8dfbc13f6b69') { Fail "Admiral Trader product/owner/trader identity mismatch" }
    if ([string]$adapter.AttributionConfidence -ne 'ExplicitAdapter') { Fail "Admiral Trader attribution must remain ExplicitAdapter" }
    if ([int]$adapter.OfferCount -ne ([int]$adapter.BaselineOfferCount + [int]$adapter.RelationshipOfferCount + [int]$adapter.MilestoneOfferCount)) { Fail "Admiral Trader stock-class counts do not cover every permanent offer" }
    if ([int]$adapter.OfferCount -ne [int]$adapter.BoundedRenewableOfferCount) { Fail "all maintained Admiral Trader permanent offers must remain bounded" }
    if ($adapter.SpecialWeaponsPermanentOfferAllowed -ne $false -or $adapter.SpecialWeaponsSampleOnly -ne $true) { Fail "Special Weapons sample-only contract drifted" }
    foreach ($offer in @($adapter.Offers)) {
        if ([string]$offer.StockClass -notin @('Baseline','Relationship','Milestone')) { Fail "unclassified Admiral Trader permanent offer $($offer.OfferId)" }
        if ([string]$offer.Source.ProvenanceClass -ne 'ExplicitAdapter') { Fail "offer $($offer.OfferId) lost ExplicitAdapter provenance" }
        # RenewableSupplyBound.Bounded serializes as numeric enum value 1.
        if ([int]$offer.Capacity.SupplyBound -ne 1) { Fail "offer $($offer.OfferId) is not bounded" }
        if ([string]$offer.StockClass -eq 'Milestone' -and ([string]$offer.GateKind -ne 'Quest' -or $null -eq $offer.EffectiveGate)) { Fail "milestone offer $($offer.OfferId) lost its authored effective quest gate" }
    }
}
else {
    if ($adapter.ContractAvailable -ne $false -or [string]$adapter.ContractState -ne 'NotInstalled') { Fail "absent Admiral Trader must report clean NotInstalled optional-dependency state" }
    if ([int]$source.LoadedAdapterCount -ne 0 -or (@($source.LoadedAdapters) -contains 'com.admiralam.spt.admiraltrader')) { Fail "source-pressure evidence claims Admiral Trader adapter while Trader is not installed" }
}

$build = $runtime.BuildIdentity
$buildValid = (
    $null -ne $build -and
    [string]$build.Product -eq 'Economy Admiral' -and
    [string]$build.TargetRuntime -eq 'SPT 4.1.3' -and
    [string]$build.HeadSha -match '^[0-9a-fA-F]{40}$'
)
if (-not $buildValid) {
    Write-Host "[Economy Admiral] build identity metadata: unavailable/invalid (non-blocking for physical economy validation)" -ForegroundColor Yellow
}

if ($traderInstalled) {
    Pass "combined Economy Beta RC proven: standalone economy + strict installed Admiral Trader v4 compatibility + source-pressure/health evidence"
    Write-Host "[Economy Admiral] trader offers: baseline=$($adapter.BaselineOfferCount), relationship=$($adapter.RelationshipOfferCount), milestone=$($adapter.MilestoneOfferCount), bounded=$($adapter.BoundedRenewableOfferCount)"
}
else {
    Pass "combined Economy Beta RC proven: standalone economy + optional Admiral Trader absent + source-pressure/health evidence"
    Write-Host "[Economy Admiral] trader compatibility: optional Admiral Trader not installed"
}
if ($buildValid) {
    Write-Host "[Economy Admiral] build: $($build.HeadSha) / workflow $($build.WorkflowRunId)"
}
Write-Host "[Economy Admiral] observed items: sourcePressure=$($source.Items.Count), health=$($health.ItemCount)"
exit 0
