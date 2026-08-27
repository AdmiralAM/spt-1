param(
    [Parameter(Mandatory = $false)]
    [string]$ModPath = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
function Fail([string]$Message) { Write-Host "[Economy Admiral] GROUPED ENFORCE FAIL: $Message" -ForegroundColor Red; exit 1 }
function Pass([string]$Message) { Write-Host "[Economy Admiral] GROUPED ENFORCE PASS: $Message" -ForegroundColor Green }

$ModPath = [System.IO.Path]::GetFullPath($ModPath)
& (Join-Path $ModPath 'Validate-Enforce.ps1') -ModPath $ModPath
if ($LASTEXITCODE -ne 0) { Fail "base Enforce validator failed" }

$evidencePath = Join-Path $ModPath 'reports/economy-admiral-grouped-item-evidence.json'
if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) { Fail "missing grouped runtime evidence: $evidencePath" }
try { $evidence = Get-Content -LiteralPath $evidencePath -Raw -Encoding UTF8 | ConvertFrom-Json }
catch { Fail "invalid grouped runtime evidence JSON: $($_.Exception.Message)" }

if ($evidence.TransactionCommitted -ne $true) { Fail "grouped evidence was not produced from a committed transaction" }
if ([int]$evidence.GroupedPlannedCount -le 0) { Fail "runtime contained no grouped item mutation candidate in the committed batch" }
if ([int]$evidence.GroupedAppliedCount -ne [int]$evidence.GroupedPlannedCount) { Fail "grouped applied/planned counts disagree" }
if ([int]$evidence.TotalAppliedItemStacks -lt [int]$evidence.GroupedAppliedCount) { Fail "grouped applied count exceeds total applied item stacks" }
if (@($evidence.GroupedLabels).Count -ne [int]$evidence.GroupedAppliedCount) { Fail "grouped label count disagrees with applied count" }

Pass "same-template grouped item reward mutation proven; grouped=$($evidence.GroupedAppliedCount); totalItemStacks=$($evidence.TotalAppliedItemStacks)"
Write-Host "[Economy Admiral] grouped item reward mutations:"
foreach ($label in @($evidence.GroupedLabels)) { Write-Host "  $label" }
exit 0
