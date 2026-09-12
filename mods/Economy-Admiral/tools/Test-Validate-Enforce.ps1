$ErrorActionPreference = 'Stop'
$root = Join-Path $env:RUNNER_TEMP 'economy-admiral-enforce-validator-test'
$mod = Join-Path $root 'Economy Admiral'
$reports = Join-Path $mod 'reports'
Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $reports | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'Validate-Enforce.ps1') (Join-Path $mod 'Validate-Enforce.ps1')
function Write-Json([string]$Name, [object]$Value) { $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $reports $Name) -Encoding UTF8 }

$before = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
$after = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
$headSha = '0123456789abcdef0123456789abcdef01234567'
Write-Json 'economy-admiral-provenance-delta.json' @{ FinalQuestCount = 123 }
Write-Json 'economy-admiral-enforcement-plan.json' @{
    SchemaVersion=5; Mode='Enforce'; Preset='Normal'; SelectedPolicy='PresetNumericQuestRewardCapV1/Normal'; MutationEligibilityPolicyVersion=3
    EnforceRequested=$true; ApplyMutations=$true; PlannedMutationCount=2; MutationCount=2; TransactionCommitted=$true; TransactionRolledBack=$false; TransactionError=$null
    Candidates=@(
        @{ ProvenanceClass='PristineUnchanged'; PristineUntouched=$true; ChangedDimensions=@(); ProposedMutations=@() },
        @{ ProvenanceClass='ModAdded'; PristineUntouched=$false; ChangedDimensions=@('QuestAdded'); ProposedMutations=@(
            @{ QuestId='mod-xp'; Dimension='Experience'; PolicyId='PresetNumericQuestRewardCapV1'; Before=10000; Current=10000; Target=3000; After=3000; Applied=$true; ManualOverride=$false },
            @{ QuestId='mod-standing'; Dimension='TraderStanding'; PolicyId='PresetNumericQuestRewardCapV1'; Before=0.20; Current=0.20; Target=0.05; After=0.05; Applied=$true; ManualOverride=$false }
        ) }
    )
}
Write-Json 'economy-admiral-runtime-evidence.json' @{
    SchemaVersion=5; Mode='Enforce'; Preset='Normal'; ExpectedReportCount=7; PresentReportCount=7; AllExpectedReportsPresent=$true
    DatabaseFingerprintBefore=@{Sha256=$before}; DatabaseFingerprintAfter=@{Sha256=$after}; DatabaseUnchangedAcrossPipeline=$false; DatabaseChangeExpected=$true
    ApplyMutations=$true; DeclaredMutationCount=2; EnforcementEvidenceValid=$true; RuntimeGatePassed=$true
    BuildIdentity=@{ Product='Economy Admiral'; HeadSha=$headSha; WorkflowRunId='123456789'; ArtifactName='economy-admiral-candidate'; CompilePackageVersion='4.1.4'; TargetRuntime='SPT 4.1.4' }
    Provenance=@{ CapturePriority=1; PristineQuestCount=100; FinalQuestCount=123; ModAddedQuestCount=25; PristineModifiedQuestCount=10; PristineUnchangedQuestCount=88; RemovedPristineQuestCount=2; BaselineCaptured=$true; CountsConsistent=$true }
}

& (Join-Path $mod 'Validate-Enforce.ps1')
if ($LASTEXITCODE -ne 0) { throw "Enforce validator PASS fixture returned exit code $LASTEXITCODE" }

$planPath = Join-Path $reports 'economy-admiral-enforcement-plan.json'
$plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
$plan.Candidates[1].ProposedMutations[0].After = 2999
$plan | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $planPath -Encoding UTF8
$process = Start-Process -FilePath 'pwsh' -ArgumentList @('-NoProfile','-File',(Join-Path $mod 'Validate-Enforce.ps1')) -Wait -PassThru
if ($process.ExitCode -eq 0) { throw 'Enforce wrong-target fixture unexpectedly passed' }

$plan.Candidates[1].ProposedMutations[0].After = 3000
$plan.Candidates[0].ProposedMutations = @(@{ QuestId='pristine'; Dimension='Experience'; PolicyId='bad'; Before=100; Current=100; Target=50; After=50; Applied=$true; ManualOverride=$false })
$plan.MutationCount = 3
$plan.PlannedMutationCount = 3
$plan | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $planPath -Encoding UTF8
$manifestPath = Join-Path $reports 'economy-admiral-runtime-evidence.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.DeclaredMutationCount = 3
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$process = Start-Process -FilePath 'pwsh' -ArgumentList @('-NoProfile','-File',(Join-Path $mod 'Validate-Enforce.ps1')) -Wait -PassThru
if ($process.ExitCode -eq 0) { throw 'Enforce pristine-mutation fixture unexpectedly passed' }

# Opt-in bounded item-stack fixture: one ModAdded item mutation and one proven PristineModified item-value mutation.
Write-Json 'economy-admiral-enforcement-plan.json' @{
    SchemaVersion=6; Mode='Enforce'; Preset='Normal'; SelectedPolicy='PresetNumericQuestRewardCapV1+SingleStackItemBudgetCapV1/Normal'; MutationEligibilityPolicyVersion=4
    EnforceRequested=$true; ApplyMutations=$true; PlannedMutationCount=2; MutationCount=2; TransactionCommitted=$true; TransactionRolledBack=$false; TransactionError=$null
    Candidates=@(
        @{ ProvenanceClass='ModAdded'; PristineUntouched=$false; ChangedDimensions=@('QuestAdded'); ProposedMutations=@(
            @{ QuestId='mod-item'; Dimension='ItemRewardStackCount'; PolicyId='PresetSingleStackItemBudgetCapV1'; Before=10; Current=10; Target=4; After=4; Applied=$true; ManualOverride=$false }
        ) },
        @{ ProvenanceClass='PristineModified'; PristineUntouched=$false; ChangedDimensions=@('SuccessItemHandbookValue'); ProposedMutations=@(
            @{ QuestId='modified-item'; Dimension='ItemRewardStackCount'; PolicyId='PresetSingleStackItemBudgetCapV1'; Before=8; Current=8; Target=3; After=3; Applied=$true; ManualOverride=$false }
        ) },
        @{ ProvenanceClass='PristineUnchanged'; PristineUntouched=$true; ChangedDimensions=@(); ProposedMutations=@() }
    )
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.DeclaredMutationCount = 2
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
& (Join-Path $mod 'Validate-Enforce.ps1')
if ($LASTEXITCODE -ne 0) { throw "Enforce bounded item-stack PASS fixture returned exit code $LASTEXITCODE" }

$itemPlan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
$itemPlan.Candidates[1].ChangedDimensions = @('Experience')
$itemPlan | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $planPath -Encoding UTF8
$process = Start-Process -FilePath 'pwsh' -ArgumentList @('-NoProfile','-File',(Join-Path $mod 'Validate-Enforce.ps1')) -Wait -PassThru
if ($process.ExitCode -eq 0) { throw 'Enforce unproven PristineModified item-stack fixture unexpectedly passed' }

Write-Host '[Economy Admiral] Enforce runtime validator smoke PASS'
