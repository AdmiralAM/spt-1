param(
    [Parameter(Mandatory = $false)]
    [string]$ModPath = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
function Fail([string]$Message) { Write-Host "[Economy Admiral] ENFORCE FAIL: $Message" -ForegroundColor Red; exit 1 }
function Pass([string]$Message) { Write-Host "[Economy Admiral] ENFORCE PASS: $Message" -ForegroundColor Green }
function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Fail "missing file: $Path" }
    if ((Get-Item -LiteralPath $Path).Length -le 0) { Fail "empty file: $Path" }
    try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { Fail "invalid JSON: $Path :: $($_.Exception.Message)" }
}
function Near([double]$A, [double]$B, [double]$Tolerance) { return [Math]::Abs($A - $B) -le $Tolerance }

$ModPath = [System.IO.Path]::GetFullPath($ModPath)
$ReportsPath = Join-Path $ModPath 'reports'
$manifest = Read-Json (Join-Path $ReportsPath 'economy-admiral-runtime-evidence.json')
$plan = Read-Json (Join-Path $ReportsPath 'economy-admiral-enforcement-plan.json')
$delta = Read-Json (Join-Path $ReportsPath 'economy-admiral-provenance-delta.json')

if ($manifest.SchemaVersion -ne 4) { Fail "runtime evidence SchemaVersion must be 4" }
if ([string]$manifest.Mode -ne 'Enforce') { Fail "validator requires mode=Enforce, got $($manifest.Mode)" }
if ($manifest.ExpectedReportCount -ne 9 -or $manifest.PresentReportCount -ne 9 -or $manifest.AllExpectedReportsPresent -ne $true) { Fail "9/9 core reports are required" }
if ($manifest.ApplyMutations -ne $true) { Fail "Enforce runtime evidence says ApplyMutations=false" }
if ($manifest.EnforcementEvidenceValid -ne $true -or $manifest.RuntimeGatePassed -ne $true) { Fail "Enforce runtime evidence gate did not pass" }
if ($manifest.DatabaseChangeExpected -ne $true) { Fail "test Alpha requires a real DB change" }
if ($manifest.DatabaseUnchangedAcrossPipeline -ne $false) { Fail "Enforce test Alpha did not change the DB fingerprint" }
if ([string]$manifest.DatabaseFingerprintBefore.Sha256 -eq [string]$manifest.DatabaseFingerprintAfter.Sha256) { Fail "before/after DB fingerprints are identical" }

$provenance = $manifest.Provenance
if ($null -eq $provenance -or $provenance.BaselineCaptured -ne $true -or $provenance.CountsConsistent -ne $true) { Fail "pristine provenance evidence invalid" }
if ([int]$delta.FinalQuestCount -ne [int]$provenance.FinalQuestCount) { Fail "provenance report/manifest final quest count mismatch" }

if ($plan.SchemaVersion -ne 5 -or $plan.MutationEligibilityPolicyVersion -ne 3) { Fail "unexpected Enforce Alpha plan schema/policy version" }
if ([string]$plan.Mode -ne 'Enforce' -or $plan.EnforceRequested -ne $true -or $plan.ApplyMutations -ne $true) { Fail "plan did not execute in Enforce mode" }
if ([string]::IsNullOrWhiteSpace([string]$plan.SelectedPolicy)) { Fail "no concrete Enforce policy selected" }
if ($plan.TransactionCommitted -ne $true -or $plan.TransactionRolledBack -ne $false) { Fail "transaction did not commit cleanly" }
if (-not [string]::IsNullOrWhiteSpace([string]$plan.TransactionError)) { Fail "transaction report contains an error: $($plan.TransactionError)" }
if ([int]$plan.MutationCount -le 0) { Fail "Alpha requires at least one applied mutation" }
if ([int]$plan.PlannedMutationCount -lt [int]$plan.MutationCount) { Fail "applied count exceeds planned count" }
if ([int]$manifest.DeclaredMutationCount -ne [int]$plan.MutationCount) { Fail "manifest mutation count disagrees with plan" }

$applied = 0
foreach ($candidate in @($plan.Candidates)) {
    $mutations = @($candidate.ProposedMutations)
    if ([string]$candidate.ProvenanceClass -eq 'PristineUnchanged') {
        if ($candidate.PristineUntouched -ne $true) { Fail "PristineUnchanged candidate not marked untouched" }
        if (@($mutations | Where-Object { $_.Applied -eq $true }).Count -ne 0) { Fail "PristineUnchanged candidate was mutated" }
    }
    if ([string]$candidate.ProvenanceClass -eq 'PristineModified') {
        foreach ($mutation in @($mutations | Where-Object { $_.Applied -eq $true })) {
            if (-not (@($candidate.ChangedDimensions) -contains [string]$mutation.Dimension)) { Fail "PristineModified mutation dimension $($mutation.Dimension) is not proven changed" }
        }
    }
    if ([string]$candidate.ProvenanceClass -notin @('ModAdded','PristineModified','PristineUnchanged')) {
        if (@($mutations | Where-Object { $_.Applied -eq $true }).Count -ne 0) { Fail "unknown provenance candidate was mutated" }
    }

    foreach ($mutation in $mutations) {
        if ($mutation.Applied -ne $true) { continue }
        $applied++
        if ([string]$mutation.Dimension -notin @('Experience','TraderStanding')) { Fail "unsupported applied dimension $($mutation.Dimension)" }
        if ([string]::IsNullOrWhiteSpace([string]$mutation.PolicyId)) { Fail "applied mutation has no policy id" }
        if (-not (Near ([double]$mutation.Before) ([double]$mutation.Current) 0.00001)) { Fail "Before/Current mismatch on $($mutation.QuestId) $($mutation.Dimension)" }
        $tolerance = if ([string]$mutation.Dimension -eq 'Experience') { 0.001 } else { 0.00001 }
        if (-not (Near ([double]$mutation.After) ([double]$mutation.Target) $tolerance)) { Fail "After != Target on $($mutation.QuestId) $($mutation.Dimension)" }
        if (Near ([double]$mutation.Before) ([double]$mutation.After) $tolerance) { Fail "applied mutation did not change value" }
        if ($mutation.ManualOverride -ne $true -and [Math]::Abs([double]$mutation.After) -gt [Math]::Abs([double]$mutation.Before) + $tolerance) { Fail "automatic policy increased reward magnitude" }
    }
}
if ($applied -ne [int]$plan.MutationCount) { Fail "applied record count $applied disagrees with MutationCount $($plan.MutationCount)" }

$build = $manifest.BuildIdentity
if ($null -eq $build -or [string]$build.Product -ne 'Economy Admiral' -or [string]$build.TargetRuntime -ne 'SPT 4.1.3') { Fail "packaged build identity invalid" }
if ([string]$build.HeadSha -notmatch '^[0-9a-fA-F]{40}$') { Fail "BuildIdentity.HeadSha is not a full SHA" }

Pass "real XP/TraderStanding DB mutation proven; applied=$applied; fingerprint changed; pristine protection and exact targets verified"
Write-Host "[Economy Admiral] build: $($build.HeadSha) / workflow $($build.WorkflowRunId)"
Write-Host "[Economy Admiral] policy: $($plan.SelectedPolicy)"
exit 0
