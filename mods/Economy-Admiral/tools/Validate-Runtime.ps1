param(
    [Parameter(Mandatory = $false)]
    [string]$ModPath = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Host "[Economy Admiral] FAIL: $Message" -ForegroundColor Red
    exit 1
}

function Pass([string]$Message) {
    Write-Host "[Economy Admiral] PASS: $Message" -ForegroundColor Green
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "missing file: $Path"
    }
    $item = Get-Item -LiteralPath $Path
    if ($item.Length -le 0) {
        Fail "empty file: $Path"
    }
    try {
        return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        Fail "invalid JSON: $Path :: $($_.Exception.Message)"
    }
}

$ModPath = [System.IO.Path]::GetFullPath($ModPath)
$ReportsPath = Join-Path $ModPath 'reports'
$ManifestPath = Join-Path $ReportsPath 'economy-admiral-runtime-evidence.json'

$expectedReports = @(
    'economy-admiral-audit.json',
    'economy-admiral-reward-utility.json',
    'economy-admiral-progression-graph.json',
    'economy-admiral-quest-constraints.json',
    'economy-admiral-quest-analysis.json',
    'economy-admiral-composite-candidates.json',
    'economy-admiral-target-proposals.json',
    'economy-admiral-enforcement-plan.json'
)

Write-Host "[Economy Admiral] validating runtime evidence in: $ReportsPath"

$manifest = Read-Json $ManifestPath
if ($manifest.SchemaVersion -ne 1) { Fail "runtime evidence SchemaVersion must be 1" }
if ($manifest.ExpectedReportCount -ne 8) { Fail "ExpectedReportCount must be 8" }
if ($manifest.PresentReportCount -ne 8) { Fail "PresentReportCount is $($manifest.PresentReportCount), expected 8" }
if ($manifest.AllExpectedReportsPresent -ne $true) { Fail "AllExpectedReportsPresent is not true" }
if ($manifest.DatabaseUnchangedAcrossPipeline -ne $true) { Fail "DatabaseUnchangedAcrossPipeline is not true" }
if ($manifest.ApplyMutations -ne $false) { Fail "runtime evidence says ApplyMutations=true" }
if ($manifest.DeclaredMutationCount -ne 0) { Fail "DeclaredMutationCount is $($manifest.DeclaredMutationCount), expected 0" }
if ($manifest.RuntimeGatePassed -ne $true) { Fail "RuntimeGatePassed is not true" }

$beforeHash = [string]$manifest.DatabaseFingerprintBefore.Sha256
$afterHash = [string]$manifest.DatabaseFingerprintAfter.Sha256
if ([string]::IsNullOrWhiteSpace($beforeHash) -or [string]::IsNullOrWhiteSpace($afterHash)) { Fail "missing DB fingerprint hash" }
if ($beforeHash -ne $afterHash) { Fail "before/after DB fingerprints differ" }

foreach ($fileName in $expectedReports) {
    $path = Join-Path $ReportsPath $fileName
    [void](Read-Json $path)
}

$audit = Read-Json (Join-Path $ReportsPath 'economy-admiral-audit.json')
if ($audit.EnforcementApplied -ne $false) { Fail "audit report says EnforcementApplied=true" }

$composite = Read-Json (Join-Path $ReportsPath 'economy-admiral-composite-candidates.json')
if ($null -ne $composite.SelectedCandidate) { Fail "composite policy candidate was selected unexpectedly" }
if ($composite.AffectsRewardAllowance -ne $false) { Fail "composite candidate affects reward allowance" }
if ($composite.AffectsEnforcement -ne $false) { Fail "composite candidate affects enforcement" }

$targets = Read-Json (Join-Path $ReportsPath 'economy-admiral-target-proposals.json')
if ($targets.ProposalsAreMutations -ne $false) { Fail "target proposals are marked as mutations" }
if ($targets.ApplyMutations -ne $false) { Fail "target proposals say ApplyMutations=true" }
if ($null -ne $targets.SelectedCompositePolicy) { Fail "target proposals selected a composite policy unexpectedly" }
foreach ($candidate in @($targets.Candidates)) {
    if ($candidate.AutomaticMutationAllowed -ne $false) { Fail "target candidate allows automatic mutation" }
    if ($null -ne $candidate.ProposedMutation) { Fail "target candidate contains a ProposedMutation" }
}

$plan = Read-Json (Join-Path $ReportsPath 'economy-admiral-enforcement-plan.json')
if ($plan.ApplyMutations -ne $false) { Fail "enforcement plan says ApplyMutations=true" }
if ($plan.MutationCount -ne 0) { Fail "enforcement plan MutationCount is $($plan.MutationCount), expected 0" }
foreach ($candidate in @($plan.Candidates)) {
    if ($candidate.AutomaticMutationAllowed -ne $false) { Fail "enforcement candidate allows automatic mutation" }
    if ($null -ne $candidate.ProposedMutation) { Fail "enforcement candidate contains a ProposedMutation" }
}

Pass "runtime gate is valid; DB fingerprint unchanged; 8/8 reports present; zero mutations declared"
Write-Host "[Economy Admiral] fingerprint: $beforeHash"
Write-Host "[Economy Admiral] mode/preset: $($manifest.Mode) / $($manifest.Preset)"
exit 0
