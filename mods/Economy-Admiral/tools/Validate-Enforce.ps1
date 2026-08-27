param([Parameter(Mandatory=$false)][string]$ModPath=$PSScriptRoot)
$ErrorActionPreference='Stop'
function Fail([string]$Message){Write-Host "[Economy Admiral] ENFORCE FAIL: $Message" -ForegroundColor Red; exit 1}
function Pass([string]$Message){Write-Host "[Economy Admiral] ENFORCE PASS: $Message" -ForegroundColor Green}
function Read-Json([string]$Path){if(-not(Test-Path -LiteralPath $Path -PathType Leaf)){Fail "missing file: $Path"}; try{return Get-Content -LiteralPath $Path -Raw -Encoding UTF8|ConvertFrom-Json}catch{Fail "invalid JSON: $Path :: $($_.Exception.Message)"}}
function Near([double]$A,[double]$B,[double]$Tolerance){[Math]::Abs($A-$B)-le $Tolerance}

$ModPath=[IO.Path]::GetFullPath($ModPath); $ReportsPath=Join-Path $ModPath 'reports'
$manifest=Read-Json (Join-Path $ReportsPath 'economy-admiral-runtime-evidence.json')
$plan=Read-Json (Join-Path $ReportsPath 'economy-admiral-enforcement-plan.json')
$delta=Read-Json (Join-Path $ReportsPath 'economy-admiral-provenance-delta.json')
if($manifest.SchemaVersion-ne 5){Fail 'runtime evidence SchemaVersion must be 5'}
if([string]$manifest.Mode-ne 'Enforce'){Fail "validator requires mode=Enforce, got $($manifest.Mode)"}
if($manifest.ExpectedReportCount-ne 7-or$manifest.PresentReportCount-ne 7-or$manifest.AllExpectedReportsPresent-ne $true){Fail '7/7 core reports are required'}
if($manifest.ApplyMutations-ne $true-or$manifest.EnforcementEvidenceValid-ne $true-or$manifest.RuntimeGatePassed-ne $true){Fail 'Enforce runtime evidence gate did not pass'}
if($manifest.DatabaseChangeExpected-ne $true-or$manifest.DatabaseUnchangedAcrossPipeline-ne $false){Fail 'Enforce test did not change DB as expected'}
if([string]$manifest.DatabaseFingerprintBefore.Sha256-eq[string]$manifest.DatabaseFingerprintAfter.Sha256){Fail 'before/after DB fingerprints are identical'}
$provenance=$manifest.Provenance; if($null-eq$provenance-or$provenance.BaselineCaptured-ne $true-or$provenance.CountsConsistent-ne $true){Fail 'pristine provenance evidence invalid'}
if([int]$delta.FinalQuestCount-ne[int]$provenance.FinalQuestCount){Fail 'provenance report/manifest final quest count mismatch'}

$itemStackMode=([int]$plan.SchemaVersion-eq 6-and[int]$plan.MutationEligibilityPolicyVersion-eq 4)
$alphaMode=([int]$plan.SchemaVersion-eq 5-and[int]$plan.MutationEligibilityPolicyVersion-eq 3)
if(-not($alphaMode-or$itemStackMode)){Fail 'unexpected Enforce plan schema/policy version'}
if([string]$plan.Mode-ne 'Enforce'-or$plan.EnforceRequested-ne $true-or$plan.ApplyMutations-ne $true){Fail 'plan did not execute in Enforce mode'}
if([string]::IsNullOrWhiteSpace([string]$plan.SelectedPolicy)){Fail 'no concrete Enforce policy selected'}
if($itemStackMode-and[string]$plan.SelectedPolicy-notlike 'PresetNumericQuestRewardCapV1+SingleStackItemBudgetCapV1/*'){Fail 'item-stack schema did not select bounded item-stack policy'}
if($plan.TransactionCommitted-ne $true-or$plan.TransactionRolledBack-ne $false){Fail 'transaction did not commit cleanly'}
if(-not[string]::IsNullOrWhiteSpace([string]$plan.TransactionError)){Fail "transaction report contains an error: $($plan.TransactionError)"}
if([int]$plan.MutationCount-le 0){Fail 'Alpha requires at least one applied mutation'}
if([int]$manifest.DeclaredMutationCount-ne[int]$plan.MutationCount){Fail 'manifest mutation count disagrees with plan'}

$allowed=if($itemStackMode){@('Experience','TraderStanding','ItemRewardStackCount')}else{@('Experience','TraderStanding')}
$applied=0;$itemApplied=0;$itemChanges=@()
foreach($candidate in @($plan.Candidates)){
  $mutations=@($candidate.ProposedMutations)
  if([string]$candidate.ProvenanceClass-eq'PristineUnchanged'-and@($mutations|Where-Object{$_.Applied-eq$true}).Count-ne 0){Fail 'PristineUnchanged candidate was mutated'}
  if([string]$candidate.ProvenanceClass-eq'PristineModified'){
    foreach($m in @($mutations|Where-Object{$_.Applied-eq$true})){$required=if([string]$m.Dimension-eq'ItemRewardStackCount'){'SuccessItemHandbookValue'}else{[string]$m.Dimension};if(-not(@($candidate.ChangedDimensions)-contains$required)){Fail "PristineModified mutation dimension $($m.Dimension) is not proven changed via $required"}}
  }
  if([string]$candidate.ProvenanceClass-notin@('ModAdded','PristineModified','PristineUnchanged')-and@($mutations|Where-Object{$_.Applied-eq$true}).Count-ne 0){Fail 'unknown provenance candidate was mutated'}
  foreach($m in $mutations){if($m.Applied-ne$true){continue};$applied++;if([string]$m.Dimension-notin$allowed){Fail "unsupported applied dimension $($m.Dimension)"};if(-not(Near([double]$m.Before)([double]$m.Current)0.00001)){Fail 'Before/Current mismatch'};$tol=if([string]$m.Dimension-in@('Experience','ItemRewardStackCount')){0.001}else{0.00001};if(-not(Near([double]$m.After)([double]$m.Target)$tol)){Fail "After != Target on $($m.QuestId) $($m.Dimension)"};if(Near([double]$m.Before)([double]$m.After)$tol){Fail 'applied mutation did not change value'};if($m.ManualOverride-ne$true-and[Math]::Abs([double]$m.After)-gt[Math]::Abs([double]$m.Before)+$tol){Fail 'automatic policy increased reward magnitude'};if([string]$m.Dimension-eq'ItemRewardStackCount'){$itemApplied++;if(-not$itemStackMode){Fail 'ItemRewardStackCount appeared under Alpha-only schema'};if([string]$m.PolicyId-ne'PresetSingleStackItemBudgetCapV1'){Fail "item stack mutation used unexpected policy $($m.PolicyId)"};$itemChanges+=[pscustomobject]@{QuestId=[string]$candidate.QuestId;QuestName=[string]$candidate.QuestName;Provenance=[string]$candidate.ProvenanceClass;Before=[double]$m.Before;After=[double]$m.After}}}
}
if($applied-ne[int]$plan.MutationCount){Fail "applied record count $applied disagrees with MutationCount $($plan.MutationCount)"}
if($itemStackMode-and$itemApplied-le 0){Fail 'bounded item-stack runtime candidate did not apply any ItemRewardStackCount mutation'}

$groupedPath=Join-Path $ReportsPath 'economy-admiral-grouped-item-evidence.json'
$grouped=$null
if(Test-Path -LiteralPath $groupedPath -PathType Leaf){
  $grouped=Read-Json $groupedPath
  if($grouped.TransactionCommitted-ne$true){Fail 'grouped evidence was not produced from a committed transaction'}
  if([int]$grouped.GroupedPlannedCount-le 0){Fail 'current grouped-item runtime candidate applied no grouped reward mutation'}
  if([int]$grouped.GroupedAppliedCount-ne[int]$grouped.GroupedPlannedCount){Fail 'grouped applied/planned counts disagree'}
  if([int]$grouped.TotalAppliedItemStacks-lt[int]$grouped.GroupedAppliedCount){Fail 'grouped count exceeds total item mutations'}
}

$build=$manifest.BuildIdentity;if($null-eq$build-or[string]$build.Product-ne'Economy Admiral'-or[string]$build.TargetRuntime-ne'SPT 4.1.3'){Fail 'packaged build identity invalid'}
if($itemStackMode){Pass "bounded item-stack runtime mutation proven; totalApplied=$applied; itemStacks=$itemApplied; fingerprint changed; pristine protection and exact targets verified";Write-Host '[Economy Admiral] applied item reward stack changes:';foreach($c in$itemChanges){Write-Host("  {0} | {1} | {2} | {3} -> {4}"-f$c.QuestId,$c.QuestName,$c.Provenance,$c.Before,$c.After)}}else{Pass "real XP/TraderStanding DB mutation proven; applied=$applied; fingerprint changed; pristine protection and exact targets verified"}
if($null-ne$grouped){Pass "same-template grouped item reward mutation proven; grouped=$($grouped.GroupedAppliedCount); totalItemStacks=$($grouped.TotalAppliedItemStacks)";Write-Host '[Economy Admiral] grouped item reward mutations:';foreach($label in@($grouped.GroupedLabels)){Write-Host "  $label"}}
Write-Host "[Economy Admiral] build: $($build.HeadSha) / workflow $($build.WorkflowRunId)";Write-Host "[Economy Admiral] policy: $($plan.SelectedPolicy)";exit 0
