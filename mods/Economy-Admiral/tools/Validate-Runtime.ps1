param(
    [Parameter(Mandatory = $false)]
    [string]$ModPath = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
function Fail([string]$Message) { Write-Host "[Economy Admiral] AUDIT FAIL: $Message" -ForegroundColor Red; exit 1 }
function Pass([string]$Message) { Write-Host "[Economy Admiral] AUDIT PASS: $Message" -ForegroundColor Green }
function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Fail "missing file: $Path" }
    if ((Get-Item -LiteralPath $Path).Length -le 0) { Fail "empty file: $Path" }
    try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { Fail "invalid JSON: $Path :: $($_.Exception.Message)" }
}

$ModPath = [System.IO.Path]::GetFullPath($ModPath)
$ReportsPath = Join-Path $ModPath 'reports'
$manifest = Read-Json (Join-Path $ReportsPath 'economy-admiral-runtime-evidence.json')
$plan = Read-Json (Join-Path $ReportsPath 'economy-admiral-enforcement-plan.json')
$delta = Read-Json (Join-Path $ReportsPath 'economy-admiral-provenance-delta.json')

if ($manifest.SchemaVersion -ne 5) { Fail "runtime evidence SchemaVersion must be 5" }
if ([string]$manifest.Mode -ne 'Audit') { Fail "validator requires mode=Audit, got $($manifest.Mode)" }
if ($manifest.ExpectedReportCount -ne 7 -or $manifest.PresentReportCount -ne 7 -or $manifest.AllExpectedReportsPresent -ne $true) { Fail "7/7 core reports are required" }
if ($manifest.DatabaseUnchangedAcrossPipeline -ne $true) { Fail "Audit changed the final DB fingerprint" }
if ($manifest.DatabaseChangeExpected -ne $false) { Fail "Audit must not expect a DB change" }
if ($manifest.ApplyMutations -ne $false) { Fail "Audit says ApplyMutations=true" }
if ([int]$manifest.DeclaredMutationCount -ne 0) { Fail "Audit declared mutations: $($manifest.DeclaredMutationCount)" }
if ($manifest.EnforcementEvidenceValid -ne $true -or $manifest.RuntimeGatePassed -ne $true) { Fail "Audit runtime evidence gate did not pass" }
if ([string]$manifest.DatabaseFingerprintBefore.Sha256 -ne [string]$manifest.DatabaseFingerprintAfter.Sha256) { Fail "Audit before/after fingerprints differ" }

$provenance = $manifest.Provenance
if ($null -eq $provenance -or $provenance.BaselineCaptured -ne $true -or $provenance.CountsConsistent -ne $true) { Fail "pristine provenance evidence invalid" }
if (([int]$provenance.ModAddedQuestCount + [int]$provenance.PristineModifiedQuestCount + [int]$provenance.PristineUnchangedQuestCount) -ne [int]$provenance.FinalQuestCount) { Fail "final provenance partition inconsistent" }
if ([int]$delta.FinalQuestCount -ne [int]$provenance.FinalQuestCount) { Fail "provenance report/manifest final quest count mismatch" }

if ($plan.SchemaVersion -ne 5 -or $plan.MutationEligibilityPolicyVersion -ne 3) { Fail "unexpected Enforce Alpha plan schema/policy version" }
if ([string]$plan.Mode -ne 'Audit') { Fail "enforcement plan is not an Audit preview" }
if ($plan.ApplyMutations -ne $false -or [int]$plan.MutationCount -ne 0) { Fail "Audit enforcement plan applied mutations" }
if ($plan.TransactionCommitted -ne $false -or $plan.TransactionRolledBack -ne $false) { Fail "Audit must not execute a transaction" }
if ([string]::IsNullOrWhiteSpace([string]$plan.SelectedPolicy)) { Fail "Audit preview did not select a concrete policy" }

$previewCount = 0
foreach ($candidate in @($plan.Candidates)) {
    if ([string]$candidate.ProvenanceClass -eq 'PristineUnchanged') {
        if ($candidate.PristineUntouched -ne $true -or @($candidate.ProposedMutations).Count -ne 0) { Fail "PristineUnchanged candidate is not protected" }
    }
    foreach ($mutation in @($candidate.ProposedMutations)) {
        $previewCount++
        if ([string]$mutation.Dimension -notin @('Experience','TraderStanding')) { Fail "Audit preview contains unsupported mutation dimension $($mutation.Dimension)" }
        if ($mutation.Applied -ne $false) { Fail "Audit preview marks mutation as applied" }
        if ([double]$mutation.After -ne [double]$mutation.Before) { Fail "Audit preview changed After value" }
        if ([string]::IsNullOrWhiteSpace([string]$mutation.PolicyId)) { Fail "Audit preview mutation has no policy id" }
    }
}
if ($previewCount -ne [int]$plan.PlannedMutationCount) { Fail "planned mutation count does not match preview records" }

$build = $manifest.BuildIdentity
if ($null -eq $build -or [string]$build.Product -ne 'Economy Admiral' -or [string]$build.TargetRuntime -ne 'SPT 4.1.4') { Fail "packaged build identity invalid" }
if ([string]$build.HeadSha -notmatch '^[0-9a-fA-F]{40}$') { Fail "BuildIdentity.HeadSha is not a full SHA" }

Pass "read-only fingerprint + concrete policy preview + pristine protection proven; planned=$previewCount, applied=0"
Write-Host "[Economy Admiral] build: $($build.HeadSha) / workflow $($build.WorkflowRunId)"
Write-Host "[Economy Admiral] mode/preset: $($manifest.Mode) / $($manifest.Preset)"
exit 0
