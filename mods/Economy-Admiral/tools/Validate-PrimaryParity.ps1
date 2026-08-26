param(
    [Parameter(Mandatory = $false)]
    [string]$ModPath = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
function Fail([string]$Message) { Write-Host "[Economy Admiral] PARITY FAIL: $Message" -ForegroundColor Red; exit 1 }
function Pass([string]$Message) { Write-Host "[Economy Admiral] PARITY PASS: $Message" -ForegroundColor Green }

$ModPath = [System.IO.Path]::GetFullPath($ModPath)
$reportPath = Join-Path (Join-Path $ModPath 'reports') 'economy-admiral-primary-parity.json'
if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) { Fail "missing report: $reportPath" }
if ((Get-Item -LiteralPath $reportPath).Length -le 0) { Fail "empty parity report" }

try { $report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json }
catch { Fail "invalid parity JSON: $($_.Exception.Message)" }

if ([int]$report.SchemaVersion -ne 1) { Fail "SchemaVersion must be 1" }
if ([string]$report.ExpectedSource -ne 'TypedFinalDbPlusPristineStartupSnapshot') { Fail "unexpected ExpectedSource: $($report.ExpectedSource)" }
if ([int]$report.FinalQuestCount -le 0) { Fail "FinalQuestCount must be positive" }
if ([int]$report.PristineQuestCount -le 0) { Fail "PristineQuestCount must be positive" }
if ([int]$report.ComparedQuestRows -ne [int]$report.FinalQuestCount) { Fail "ComparedQuestRows=$($report.ComparedQuestRows) but FinalQuestCount=$($report.FinalQuestCount)" }
if ([int]$report.ExpectedQuestRewardSourceEdges -ne [int]$report.ReportedQuestRewardSourceEdges) { Fail "quest reward edge parity failed: expected=$($report.ExpectedQuestRewardSourceEdges), reported=$($report.ReportedQuestRewardSourceEdges)" }
if ($report.QuestRowsMatch -ne $true) { Fail "QuestRowsMatch is not true" }
if ($report.AcquisitionMatches -ne $true) { Fail "AcquisitionMatches is not true" }
if ($report.BenchmarkMatches -ne $true) { Fail "BenchmarkMatches is not true" }
if ($report.AllMatched -ne $true) { Fail "AllMatched is not true" }
if (@($report.Mismatches).Count -ne 0) { Fail "Mismatches is not empty" }

Pass "typed final DB + pristine startup primary audit parity proven"
Write-Host "[Economy Admiral] parity quests: final=$($report.FinalQuestCount), pristine=$($report.PristineQuestCount), compared=$($report.ComparedQuestRows)"
Write-Host "[Economy Admiral] parity questRewardEdges: $($report.ExpectedQuestRewardSourceEdges)"
exit 0
