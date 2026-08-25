param(
    [Parameter(Mandatory = $false)]
    [string]$ModPath = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
function Fail([string]$Message) { Write-Host "[Economy Admiral] FAIL: $Message" -ForegroundColor Red; exit 1 }
function Pass([string]$Message) { Write-Host "[Economy Admiral] PASS: $Message" -ForegroundColor Green }
function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Fail "missing file: $Path" }
    $item = Get-Item -LiteralPath $Path
    if ($item.Length -le 0) { Fail "empty file: $Path" }
    try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { Fail "invalid JSON: $Path :: $($_.Exception.Message)" }
}

$ModPath = [System.IO.Path]::GetFullPath($ModPath)
$ReportsPath = Join-Path $ModPath 'reports'
$ManifestPath = Join-Path $ReportsPath 'economy-admiral-runtime-evidence.json'
$expectedReports = @(
    'economy-admiral-audit.json', 'economy-admiral-reward-utility.json',
    'economy-admiral-progression-graph.json', 'economy-admiral-quest-constraints.json',
    'economy-admiral-quest-analysis.json', 'economy-admiral-composite-candidates.json',
    'economy-admiral-target-proposals.json', 'economy-admiral-enforcement-plan.json'
)

Write-Host "[Economy Admiral] validating runtime evidence in: $ReportsPath"
$manifest = Read-Json $ManifestPath

if ($manifest.SchemaVersion -ne 3) { Fail "runtime evidence SchemaVersion must be 3" }
if ($manifest.ExpectedReportCount -ne 8) { Fail "ExpectedReportCount must be 8" }
if ($manifest.PresentReportCount -ne 8) { Fail "PresentReportCount is $($manifest.PresentReportCount), expected 8" }
if ($manifest.AllExpectedReportsPresent -ne $true) { Fail "AllExpectedReportsPresent is not true" }
if ($manifest.DatabaseUnchangedAcrossPipeline -ne $true) { Fail "DatabaseUnchangedAcrossPipeline is not true" }
if ($manifest.ApplyMutations -ne $false) { Fail "runtime evidence says ApplyMutations=true" }
if ($manifest.DeclaredMutationCount -ne 0) { Fail "DeclaredMutationCount is $($manifest.DeclaredMutationCount), expected 0" }
if ($manifest.RuntimeGatePassed -ne $true) { Fail "RuntimeGatePassed is not true" }

$provenance = $manifest.Provenance
if ($null -eq $provenance) { Fail "Provenance is missing" }
if ($provenance.CapturePriority -ne 1) { Fail "unexpected pristine capture priority: $($provenance.CapturePriority)" }
if ($provenance.BaselineCaptured -ne $true) { Fail "BaselineCaptured is not true" }
if ($provenance.BaselineNotLargerThanFinal -ne $true) { Fail "BaselineNotLargerThanFinal is not true" }
if ([int]$provenance.PristineQuestCount -le 0) { Fail "PristineQuestCount must be positive" }
if ([int]$provenance.FinalQuestCount -lt [int]$provenance.PristineQuestCount) { Fail "FinalQuestCount is smaller than pristine baseline" }
if ([int]$provenance.ModAddedQuestCount -ne ([int]$provenance.FinalQuestCount - [int]$provenance.PristineQuestCount)) { Fail "ModAddedQuestCount is inconsistent" }

$build = $manifest.BuildIdentity
if ($null -eq $build) { Fail "BuildIdentity is missing; use the packaged CI candidate" }
if ([string]$build.Product -ne 'Economy Admiral') { Fail "unexpected BuildIdentity.Product: $($build.Product)" }
if ([string]$build.ArtifactName -ne 'economy-admiral-candidate') { Fail "unexpected ArtifactName: $($build.ArtifactName)" }
if ([string]$build.CompilePackageVersion -ne '4.1.2') { Fail "unexpected compile package version: $($build.CompilePackageVersion)" }
if ([string]$build.TargetRuntime -ne 'SPT 4.1.3') { Fail "unexpected target runtime: $($build.TargetRuntime)" }
if ([string]$build.HeadSha -notmatch '^[0-9a-fA-F]{40}$') { Fail "BuildIdentity.HeadSha is not a full commit SHA" }
if ([string]::IsNullOrWhiteSpace([string]$build.WorkflowRunId)) { Fail "BuildIdentity.WorkflowRunId is missing" }

$beforeHash = [string]$manifest.DatabaseFingerprintBefore.Sha256
$afterHash = [string]$manifest.DatabaseFingerprintAfter.Sha256
if ([string]::IsNullOrWhiteSpace($beforeHash) -or [string]::IsNullOrWhiteSpace($afterHash)) { Fail "missing DB fingerprint hash" }
if ($beforeHash -ne $afterHash) { Fail "before/after DB fingerprints differ" }
foreach ($fileName in $expectedReports) { [void](Read-Json (Join-Path $ReportsPath $fileName)) }

$audit = Read-Json (Join-Path $ReportsPath 'economy-admiral-audit.json')
if ($audit.EnforcementApplied -ne $false) { Fail "audit report says EnforcementApplied=true" }
if ([string]$audit.VanillaBenchmarkSource -ne 'PristineStartupSnapshot') { Fail "primary audit is not bound to pristine benchmark" }

$utility = Read-Json (Join-Path $ReportsPath 'economy-admiral-reward-utility.json')
if ([string]$utility.BenchmarkSource -ne 'PristineStartupSnapshot') { Fail "reward utility is not bound to pristine benchmark" }
$progression = Read-Json (Join-Path $ReportsPath 'economy-admiral-progression-graph.json')
if ([string]$progression.BenchmarkSource -ne 'PristineStartupSnapshot') { Fail "progression graph is not bound to pristine benchmark" }
$constraints = Read-Json (Join-Path $ReportsPath 'economy-admiral-quest-constraints.json')
if ([string]$constraints.BenchmarkSource -ne 'PristineStartupSnapshot') { Fail "quest constraints are not bound to pristine benchmark" }

$analysis = Read-Json (Join-Path $ReportsPath 'economy-admiral-quest-analysis.json')
if ([string]$analysis.Note -notmatch 'pristine startup baseline') { Fail "unified analysis does not declare pristine provenance" }

$composite = Read-Json (Join-Path $ReportsPath 'economy-admiral-composite-candidates.json')
if ($null -ne $composite.SelectedCandidate) { Fail "composite policy candidate was selected unexpectedly" }
if ($composite.AffectsRewardAllowance -ne $false -or $composite.AffectsEnforcement -ne $false) { Fail "composite candidate affects policy unexpectedly" }

$targets = Read-Json (Join-Path $ReportsPath 'economy-admiral-target-proposals.json')
if ($targets.ProposalsAreMutations -ne $false -or $targets.ApplyMutations -ne $false) { Fail "target proposals are mutating" }
if ($null -ne $targets.SelectedCompositePolicy) { Fail "target proposals selected a composite policy unexpectedly" }
foreach ($candidate in @($targets.Candidates)) {
    if ($candidate.AutomaticMutationAllowed -ne $false -or $null -ne $candidate.ProposedMutation) { Fail "target candidate permits mutation" }
}

$plan = Read-Json (Join-Path $ReportsPath 'economy-admiral-enforcement-plan.json')
if ($plan.ApplyMutations -ne $false -or $plan.MutationCount -ne 0) { Fail "enforcement plan is mutating" }
foreach ($candidate in @($plan.Candidates)) {
    if ($candidate.AutomaticMutationAllowed -ne $false -or $null -ne $candidate.ProposedMutation) { Fail "enforcement candidate permits mutation" }
}

Pass "runtime gate valid; pristine baseline proven; exact CI build identified; DB unchanged; 8/8 reports present; zero mutations declared"
Write-Host "[Economy Admiral] build: $($build.HeadSha) / workflow $($build.WorkflowRunId)"
Write-Host "[Economy Admiral] provenance: pristine=$($provenance.PristineQuestCount), final=$($provenance.FinalQuestCount), modAdded=$($provenance.ModAddedQuestCount)"
Write-Host "[Economy Admiral] fingerprint: $beforeHash"
Write-Host "[Economy Admiral] mode/preset: $($manifest.Mode) / $($manifest.Preset)"
exit 0
