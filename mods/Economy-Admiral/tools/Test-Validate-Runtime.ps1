$ErrorActionPreference = 'Stop'

$root = Join-Path $env:RUNNER_TEMP 'economy-admiral-validator-test'
$mod = Join-Path $root 'Economy Admiral'
$reports = Join-Path $mod 'reports'
Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $reports | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'Validate-Runtime.ps1') (Join-Path $mod 'Validate-Runtime.ps1')

function Write-Json([string]$Name, [object]$Value) {
    $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $reports $Name) -Encoding UTF8
}

$hash = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
$headSha = '0123456789abcdef0123456789abcdef01234567'
Write-Json 'economy-admiral-audit.json' @{ EnforcementApplied = $false }
Write-Json 'economy-admiral-reward-utility.json' @{ SchemaVersion = 2 }
Write-Json 'economy-admiral-progression-graph.json' @{ SchemaVersion = 2 }
Write-Json 'economy-admiral-quest-constraints.json' @{ SchemaVersion = 1 }
Write-Json 'economy-admiral-quest-analysis.json' @{ SchemaVersion = 3 }
Write-Json 'economy-admiral-composite-candidates.json' @{
    SelectedCandidate = $null
    AffectsRewardAllowance = $false
    AffectsEnforcement = $false
}
Write-Json 'economy-admiral-target-proposals.json' @{
    ProposalsAreMutations = $false
    ApplyMutations = $false
    SelectedCompositePolicy = $null
    Candidates = @(@{ AutomaticMutationAllowed = $false; ProposedMutation = $null })
}
Write-Json 'economy-admiral-enforcement-plan.json' @{
    ApplyMutations = $false
    MutationCount = 0
    Candidates = @(@{ AutomaticMutationAllowed = $false; ProposedMutation = $null })
}
Write-Json 'economy-admiral-runtime-evidence.json' @{
    SchemaVersion = 2
    Mode = 'Audit'
    Preset = 'Normal'
    BuildIdentity = @{
        Product = 'Economy Admiral'
        HeadSha = $headSha
        WorkflowRunId = '123456789'
        ArtifactName = 'economy-admiral-candidate'
        CompilePackageVersion = '4.1.2'
        TargetRuntime = 'SPT 4.1.3'
    }
    ExpectedReportCount = 8
    PresentReportCount = 8
    AllExpectedReportsPresent = $true
    DatabaseFingerprintBefore = @{ Sha256 = $hash }
    DatabaseFingerprintAfter = @{ Sha256 = $hash }
    DatabaseUnchangedAcrossPipeline = $true
    ApplyMutations = $false
    DeclaredMutationCount = 0
    RuntimeGatePassed = $true
}

& (Join-Path $mod 'Validate-Runtime.ps1')
if ($LASTEXITCODE -ne 0) { throw "Validator PASS fixture returned exit code $LASTEXITCODE" }

$manifestPath = Join-Path $reports 'economy-admiral-runtime-evidence.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.DatabaseFingerprintAfter.Sha256 = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
$manifest.DatabaseUnchangedAcrossPipeline = $false
$manifest.RuntimeGatePassed = $false
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

$process = Start-Process -FilePath 'pwsh' -ArgumentList @('-NoProfile', '-File', (Join-Path $mod 'Validate-Runtime.ps1')) -Wait -PassThru
if ($process.ExitCode -eq 0) { throw 'Validator FAIL fixture unexpectedly returned exit code 0' }

Write-Host '[Economy Admiral] validator smoke tests PASS'
