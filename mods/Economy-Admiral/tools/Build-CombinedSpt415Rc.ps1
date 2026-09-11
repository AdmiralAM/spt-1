param(
    [Parameter(Mandatory = $true)][string]$SptRoot,
    [Parameter(Mandatory = $true)][string]$SourceHeadSha,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$economyRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $economyRoot '../..')).Path
$traderRoot = Join-Path $repoRoot 'mods/Admiral-Trader'
$head = (& git -C $repoRoot rev-parse HEAD).Trim().ToLowerInvariant()
if ($head -ne $SourceHeadSha.ToLowerInvariant()) { throw "Combined source HEAD mismatch: $head" }
$traderOutput = Join-Path $OutputDirectory 'active-trader'
& (Join-Path $traderRoot 'tools/Build-Spt415Rc.ps1') -SptRoot $SptRoot -SourceHeadSha $head -OutputDirectory $traderOutput
$traderCandidates = @(Get-ChildItem $traderOutput -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'admiral-trader-provenance.json') })
if ($traderCandidates.Count -ne 1) { throw "Expected one active Trader candidate, got $($traderCandidates.Count)" }
$traderSource = Join-Path $traderCandidates[0].FullName 'SPT_Runtime/user/mods/Admiral-Trader'

$runtimeRoot = if (Test-Path (Join-Path $SptRoot 'SPTarkov.Server.Core.dll')) { (Resolve-Path $SptRoot).Path } else { (Resolve-Path (Join-Path $SptRoot 'SPT_Runtime')).Path }
$managedRoot = Join-Path (Split-Path $runtimeRoot -Parent) 'EscapeFromTarkov_Data/Managed'
dotnet build (Join-Path $economyRoot 'server/Economy-Admiral.csproj') -c Release --nologo "-p:SptRuntimeLibDir=$runtimeRoot"
if ($LASTEXITCODE -ne 0) { throw 'Economy exact-runtime server build failed.' }
if (Test-Path (Join-Path $managedRoot 'UnityEngine.CoreModule.dll') -PathType Leaf) {
    dotnet build (Join-Path $economyRoot 'client/Economy-Admiral.Client.csproj') -c Release --nologo "-p:UnityManagedLibDir=$managedRoot"
} else {
    dotnet build (Join-Path $economyRoot 'client/Economy-Admiral.Client.csproj') -c Release --nologo
}
if ($LASTEXITCODE -ne 0) { throw 'Economy client build failed.' }

$packageRoot = Join-Path $OutputDirectory "Admiral-Trader-0.2.0-Economy-0.1.0-SPT415-Stable-$head"
if (Test-Path $packageRoot) { Remove-Item $packageRoot -Recurse -Force }
$traderTarget = Join-Path $packageRoot 'SPT_Runtime/user/mods/Admiral-Trader'
$economyTarget = Join-Path $packageRoot 'SPT_Runtime/user/mods/Economy Admiral'
$clientTarget = Join-Path $packageRoot 'BepInEx/plugins/Economy Admiral'
New-Item (Join-Path $economyTarget 'config') -ItemType Directory -Force | Out-Null
New-Item $clientTarget -ItemType Directory -Force | Out-Null
Copy-Item $traderSource $traderTarget -Recurse
Copy-Item (Join-Path $economyRoot 'server/bin/Release/net10.0/Economy-Admiral.dll') $economyTarget
Copy-Item (Join-Path $economyRoot 'client/bin/Release/netstandard2.1/Economy Admiral v0.1.0.dll') $clientTarget
Copy-Item (Join-Path $economyRoot 'config/config.json') (Join-Path $economyTarget 'config/config.default.json')
Copy-Item (Join-Path $economyRoot 'README.md') $economyTarget

$quests = @(Get-ChildItem (Join-Path $traderTarget 'db/quests') -Filter '*.json' -File)
$assort = Get-Content (Join-Path $traderTarget 'db/assort.json') -Raw | ConvertFrom-Json
if ($quests.Count -ne 43 -or @($assort.items | Where-Object parentId -eq 'hideout').Count -ne 37) { throw 'Combined package does not contain the stabilized active Trader scope.' }
$config = Get-Content (Join-Path $economyTarget 'config/config.default.json') -Raw | ConvertFrom-Json
if ($config.mode -ne 'Enforce' -or $config.preset -ne 'Normal') { throw 'Economy defaults drifted from Normal/Enforce.' }

[ordered]@{
    schemaVersion = 2; product = 'Admiral Trader + Economy Admiral'; sourceHeadSha = $head
    targetSptVersion = '4.1.5'; sptCompatibility = '~4.1.0'; releaseChannel = 'stable'
    trader = [ordered]@{ version='0.2.0'; traderId='d5c27bb3169f8dfbc13f6b69'; questCount=43; totalFiniteOffers=37 }
    economy = [ordered]@{ version='0.1.0'; defaultMode='Enforce'; recommendedPreset='Normal'; ownsTraderEngine=$false }
} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $packageRoot 'admiral-combined-provenance.json') -Encoding utf8

$zip = "$packageRoot.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content "$zip.sha256" -Encoding ascii
Write-Host "Combined candidate: $zip"
Write-Host "Combined candidate SHA-256: $hash"
