$ErrorActionPreference = 'Stop'
$root = Join-Path ([System.IO.Path]::GetTempPath()) ("economy-admiral-beta-smoke-" + [Guid]::NewGuid().ToString('N'))
$reports = Join-Path $root 'reports'
New-Item -ItemType Directory -Force -Path $reports | Out-Null
try {
    @'
param([string]$ModPath)
Write-Host "synthetic Enforce validator PASS"
exit 0
'@ | Set-Content (Join-Path $root 'Validate-Enforce.ps1') -Encoding UTF8

    $source = [ordered]@{
        SchemaVersion = 2
        EvidenceCoverage = 'FinalDbCore+ExplicitAdaptersWithExplicitUnknownChannels'
        LoadedAdapterCount = 1
        SourceCount = 2
        CapacityEvidenceCount = 1
        LoadedAdapters = @('com.admiralam.spt.admiraltrader')
        ChannelCoverage = @([ordered]@{ Channel = 6; State = 'UnknownNoMaintainedAdapter'; ObservedSourceCount = 0 })
        Items = @([ordered]@{ ItemTemplateId = 'tpl1' })
    }
    $source | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $reports 'economy-admiral-source-pressure.json') -Encoding UTF8

    $health = [ordered]@{
        SchemaVersion = 1
        SourcePressureSchemaVersion = 2
        CompositeScoreSelected = $false
        MutationAuthorized = $false
        ItemCount = 1
        ChannelCoverage = @([ordered]@{ Channel = 6; State = 'UnknownNoMaintainedAdapter'; ObservedSourceCount = 0 })
    }
    $health | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $reports 'economy-admiral-health.json') -Encoding UTF8

    $offer = [ordered]@{
        OfferId = 'mile1'
        StockClass = 'Milestone'
        GateKind = 'Quest'
        EffectiveGate = [ordered]@{ QuestId = 'q1'; EffectiveProgressionLevel = 12 }
        Source = [ordered]@{ ProvenanceClass = 'ExplicitAdapter' }
        Capacity = [ordered]@{ SupplyBound = 1 }
    }
    $adapter = [ordered]@{
        SchemaVersion = 3
        Installed = $true
        ContractAvailable = $true
        ContractState = 'LoadedGameplayAlphaV4'
        ProductName = 'Admiral Trader'
        ModGuid = 'com.admiralam.spt.admiraltrader'
        TraderId = 'd5c27bb3169f8dfbc13f6b69'
        GameplayPolicySchemaVersion = 4
        AttributionConfidence = 'ExplicitAdapter'
        OfferCount = 1
        BaselineOfferCount = 0
        RelationshipOfferCount = 0
        MilestoneOfferCount = 1
        BoundedRenewableOfferCount = 1
        SpecialWeaponsPermanentOfferAllowed = $false
        SpecialWeaponsSampleOnly = $true
        Offers = @($offer)
    }
    $adapter | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $reports 'economy-admiral-admiral-trader-adapter.json') -Encoding UTF8

    $runtime = [ordered]@{
        BuildIdentity = [ordered]@{
            Product = 'Economy Admiral'
            TargetRuntime = 'SPT 4.1.3'
            HeadSha = '0123456789abcdef0123456789abcdef01234567'
            WorkflowRunId = '123'
        }
    }
    $runtime | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $reports 'economy-admiral-runtime-evidence.json') -Encoding UTF8

    $validator = Join-Path $PSScriptRoot 'Validate-Beta.ps1'

    # Installed + supported Trader must PASS under the strict adapter contract.
    & pwsh -NoProfile -File $validator -ModPath $root
    if ($LASTEXITCODE -ne 0) { throw "installed supported Trader Beta fixture failed" }

    # Build identity metadata is diagnostic only; invalid metadata must not block physical economy validation.
    $runtime.BuildIdentity = $null
    $runtime | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $reports 'economy-admiral-runtime-evidence.json') -Encoding UTF8
    $identityOutput = & pwsh -NoProfile -File $validator -ModPath $root 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "invalid/missing build identity incorrectly blocked Beta validator" }
    if ($identityOutput -notmatch 'build identity metadata: unavailable/invalid') { throw "invalid build identity did not emit non-blocking diagnostic" }

    # Trader absent is a valid standalone Economy Admiral RC state.
    $source.LoadedAdapterCount = 0
    $source.LoadedAdapters = @()
    $source | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $reports 'economy-admiral-source-pressure.json') -Encoding UTF8
    $adapter.Installed = $false
    $adapter.ContractAvailable = $false
    $adapter.ContractState = 'NotInstalled'
    $adapter.GameplayPolicySchemaVersion = 0
    $adapter.OfferCount = 0
    $adapter.BaselineOfferCount = 0
    $adapter.RelationshipOfferCount = 0
    $adapter.MilestoneOfferCount = 0
    $adapter.BoundedRenewableOfferCount = 0
    $adapter.Offers = @()
    $adapter | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $reports 'economy-admiral-admiral-trader-adapter.json') -Encoding UTF8
    & pwsh -NoProfile -File $validator -ModPath $root
    if ($LASTEXITCODE -ne 0) { throw "standalone Economy Admiral fixture without Trader failed" }

    # If Trader is installed, unsupported/missing contract must remain fail-closed.
    $adapter.Installed = $true
    $adapter.ContractAvailable = $false
    $adapter.ContractState = 'ContractUnsupported'
    $adapter | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $reports 'economy-admiral-admiral-trader-adapter.json') -Encoding UTF8
    $negativeOutput = & pwsh -NoProfile -File $validator -ModPath $root 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw "incompatible installed Trader contract did not fail Beta validator" }
    if ($negativeOutput -notmatch 'BETA FAIL') { throw "negative Beta validator fixture did not emit explicit failure" }

    Write-Host 'Economy Admiral Beta validator optional-Trader/build-metadata smoke PASS'
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
exit 0
