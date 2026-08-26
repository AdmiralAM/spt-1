$ErrorActionPreference = 'Stop'

$root = Join-Path $env:RUNNER_TEMP 'economy-admiral-parity-validator-test'
$mod = Join-Path $root 'Economy Admiral'
$reports = Join-Path $mod 'reports'
Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $reports | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'Validate-PrimaryParity.ps1') (Join-Path $mod 'Validate-PrimaryParity.ps1')

$reportPath = Join-Path $reports 'economy-admiral-primary-parity.json'
function Write-Parity([bool]$AllMatched, [bool]$QuestRowsMatch, [bool]$AcquisitionMatches, [bool]$BenchmarkMatches, [object[]]$Mismatches) {
    [ordered]@{
        SchemaVersion = 1
        ExpectedSource = 'TypedFinalDbPlusPristineStartupSnapshot'
        FinalQuestCount = 123
        PristineQuestCount = 100
        ComparedQuestRows = 123
        ExpectedQuestRewardSourceEdges = 38138
        ReportedQuestRewardSourceEdges = 38138
        BenchmarkMatches = $BenchmarkMatches
        AcquisitionMatches = $AcquisitionMatches
        QuestRowsMatch = $QuestRowsMatch
        AllMatched = $AllMatched
        Mismatches = $Mismatches
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8
}

Write-Parity $true $true $true $true @()
& (Join-Path $mod 'Validate-PrimaryParity.ps1')
if ($LASTEXITCODE -ne 0) { throw "Parity validator PASS fixture returned exit code $LASTEXITCODE" }

Write-Parity $false $false $true $true @(@{ Scope='Quest'; SubjectId='q1'; Field='KnownHandbookValue'; Expected='100'; Actual='101' })
$process = Start-Process -FilePath 'pwsh' -ArgumentList @('-NoProfile', '-File', (Join-Path $mod 'Validate-PrimaryParity.ps1')) -Wait -PassThru
if ($process.ExitCode -eq 0) { throw 'Parity mismatch fixture unexpectedly returned exit code 0' }

Write-Parity $true $true $true $true @(@{ Scope='Quest'; SubjectId='q1'; Field='Unexpected'; Expected='x'; Actual='y' })
$process = Start-Process -FilePath 'pwsh' -ArgumentList @('-NoProfile', '-File', (Join-Path $mod 'Validate-PrimaryParity.ps1')) -Wait -PassThru
if ($process.ExitCode -eq 0) { throw 'Non-empty mismatch list unexpectedly returned exit code 0' }

Write-Parity $true $true $true $true @()
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$report.ReportedQuestRewardSourceEdges = 38137
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8
$process = Start-Process -FilePath 'pwsh' -ArgumentList @('-NoProfile', '-File', (Join-Path $mod 'Validate-PrimaryParity.ps1')) -Wait -PassThru
if ($process.ExitCode -eq 0) { throw 'Quest reward edge mismatch unexpectedly returned exit code 0' }

Write-Host '[Economy Admiral] primary parity validator smoke PASS'
