$ErrorActionPreference = 'Stop'
$root = Join-Path $env:RUNNER_TEMP 'economy-admiral-audit-validator-test'
$mod = Join-Path $root 'Economy Admiral'
$reports = Join-Path $mod 'reports'
Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $reports | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'Validate-Runtime.ps1') (Join-Path $mod 'Validate-Runtime.ps1')
function Write-Json([string]$Name, [object]$Value) { $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $reports $Name) -Encoding UTF8 }

$hash = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
$headSha = '0123456789abcdef0123456789abcdef01234567'
Write-Json 'economy-admiral-provenance-delta.json' @{ FinalQuestCount = 123 }
Write-Json 'economy-admiral-enforcement-plan.json' @{
    SchemaVersion = 5; Mode = 'Audit'; Preset = 'Normal'; SelectedPolicy = 'PresetNumericQuestRewardCapV1/Normal'; MutationEligibilityPolicyVersion = 3
    ApplyMutations = $false; PlannedMutationCount = 1; MutationCount = 0; TransactionCommitted = $false; TransactionRolledBack = $false
    Candidates = @(
        @{ ProvenanceClass='PristineUnchanged'; PristineUntouched=$true; ProposedMutations=@() },
        @{ ProvenanceClass='ModAdded'; PristineUntouched=$false; ProposedMutations=@(@{ QuestId='mod-q'; Dimension='Experience'; PolicyId='PresetNumericQuestRewardCapV1'; Before=10000; Current=10000; Target=3000; After=10000; Applied=$false; ManualOverride=$false }) }
    )
}
Write-Json 'economy-admiral-runtime-evidence.json' @{
    SchemaVersion = 5; Mode='Audit'; Preset='Normal'; ExpectedReportCount=7; PresentReportCount=7; AllExpectedReportsPresent=$true
    DatabaseFingerprintBefore=@{Sha256=$hash}; DatabaseFingerprintAfter=@{Sha256=$hash}; DatabaseUnchangedAcrossPipeline=$true; DatabaseChangeExpected=$false
    ApplyMutations=$false; DeclaredMutationCount=0; EnforcementEvidenceValid=$true; RuntimeGatePassed=$true
    BuildIdentity=@{ Product='Economy Admiral'; HeadSha=$headSha; WorkflowRunId='123456789'; ArtifactName='economy-admiral-candidate'; CompilePackageVersion='4.1.2'; TargetRuntime='SPT 4.1.3' }
    Provenance=@{ CapturePriority=1; PristineQuestCount=100; FinalQuestCount=123; ModAddedQuestCount=25; PristineModifiedQuestCount=10; PristineUnchangedQuestCount=88; RemovedPristineQuestCount=2; BaselineCaptured=$true; CountsConsistent=$true }
}

& (Join-Path $mod 'Validate-Runtime.ps1')
if ($LASTEXITCODE -ne 0) { throw "Audit validator PASS fixture returned exit code $LASTEXITCODE" }

$manifestPath = Join-Path $reports 'economy-admiral-runtime-evidence.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.DatabaseFingerprintAfter.Sha256 = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
$manifest.DatabaseUnchangedAcrossPipeline = $false
$manifest.RuntimeGatePassed = $false
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$process = Start-Process -FilePath 'pwsh' -ArgumentList @('-NoProfile','-File',(Join-Path $mod 'Validate-Runtime.ps1')) -Wait -PassThru
if ($process.ExitCode -eq 0) { throw 'Audit validator mutation fixture unexpectedly passed' }

Write-Host '[Economy Admiral] Audit runtime validator smoke PASS'
