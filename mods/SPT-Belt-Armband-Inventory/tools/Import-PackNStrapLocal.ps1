[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $SptRoot,
    [Parameter(Mandatory = $true)][string] $PackNStrapSourceRoot,
    [string] $PackNStrapRuntimeRoot,
    [string] $BackupRoot
)

$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $moduleRoot '..\..')).Path
$spt = (Resolve-Path -LiteralPath $SptRoot).Path
$source = (Resolve-Path -LiteralPath $PackNStrapSourceRoot).Path
if (-not $BackupRoot) { $BackupRoot = Join-Path $spt 'backups\BAndHB' }
$activeServerSource = Join-Path $spt 'SPT_Runtime\user\mods\WTT-PackNStrapServer'
$activeClientSource = Join-Path $spt 'BepInEx\plugins\WTT-PackNStrapClient'
$disableOriginalRuntime = $false
if ($PackNStrapRuntimeRoot) {
    $runtimeSourceRoot = (Resolve-Path -LiteralPath $PackNStrapRuntimeRoot).Path
    $serverSource = Join-Path $runtimeSourceRoot 'WTT-PackNStrapServer'
    $clientSource = Join-Path $runtimeSourceRoot 'WTT-PackNStrapClient'
} elseif ((Test-Path -LiteralPath $activeServerSource) -and (Test-Path -LiteralPath $activeClientSource)) {
    $serverSource = $activeServerSource
    $clientSource = $activeClientSource
    $disableOriginalRuntime = $true
} else {
    $runtimeSourceRoot = Get-ChildItem -LiteralPath $BackupRoot -Directory -Filter 'packnstrap-disabled-*' -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $runtimeSourceRoot) { throw "No active or preserved Pack 'n' Strap runtime was found." }
    $serverSource = Join-Path $runtimeSourceRoot 'WTT-PackNStrapServer'
    $clientSource = Join-Path $runtimeSourceRoot 'WTT-PackNStrapClient'
}
$clientCommon = Join-Path $spt 'BepInEx\plugins\WTT-ClientCommonLib\WTT-ClientCommonLib.dll'
$serverCommon = Join-Path $spt 'SPT_Runtime\user\mods\WTT-ServerCommonLib\WTT-ServerCommonLib.dll'

foreach ($required in @($serverSource, $clientSource, $clientCommon, $serverCommon)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required local Pack 'n' Strap/CommonLib input missing: $required" }
}
if (Get-Process -Name 'SPT.Server','SPT.Launcher','EscapeFromTarkov' -ErrorAction SilentlyContinue) {
    throw 'SPT Server, Launcher and EFT must be closed before private import deployment.'
}

$stage = Join-Path $moduleRoot '.local-packnstrap'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
$runtime = Join-Path $stage 'runtime'
New-Item -ItemType Directory -Force -Path $runtime | Out-Null
Copy-Item -LiteralPath (Join-Path $serverSource 'bundles') -Destination $runtime -Recurse
Copy-Item -LiteralPath (Join-Path $serverSource 'db') -Destination $runtime -Recurse
Copy-Item -LiteralPath (Join-Path $serverSource 'bundles.json') -Destination $runtime

$gearPath = Join-Path $runtime 'db\CustomItems\Gear_Belts.json'
$gear = Get-Content -Raw -LiteralPath $gearPath | ConvertFrom-Json -AsHashtable
foreach ($item in $gear.Values) { $item['addtoInventorySlots'] = @() }
$gear | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $gearPath -Encoding utf8NoBOM

$parentsPath = Join-Path $runtime 'db\CustomParents\PackNStrapParents.jsonc'
$parentsText = Get-Content -Raw -LiteralPath $parentsPath
$parentsText = $parentsText -replace '"addToInventorySlots"\s*:\s*true', '"addToInventorySlots": false'
$parentsText = $parentsText -replace '"inventorySlots"\s*:\s*\[\s*"ArmBand"\s*\]', '"inventorySlots": []'
Set-Content -LiteralPath $parentsPath -Value $parentsText -Encoding utf8NoBOM

$clientProject = Join-Path $moduleRoot 'src\SPT-Belt-Armband-Inventory.csproj'
$serverProject = Join-Path $moduleRoot 'server\SPT-Belt-Armband-Inventory.Server.csproj'
dotnet build $clientProject -c Release --no-restore -p:PackNStrapSourceRoot=$source -p:PackNStrapSptRoot=$spt -p:PackNStrapClientCommonLib=$clientCommon
if ($LASTEXITCODE -ne 0) { throw 'Private client build failed.' }
dotnet build $serverProject -c Release --no-restore -p:PackNStrapServerCommonLib=$serverCommon
if ($LASTEXITCODE -ne 0) { throw 'Private server build failed.' }

$clientDll = Join-Path $moduleRoot 'src\bin\Release\netstandard2.1\SPT Belt Armband Inventory v0.1.0.dll'
$serverDll = Join-Path $moduleRoot 'server\bin\Release\net10.0\SPT-Belt-Armband-Inventory.Server.dll'
& (Join-Path $PSScriptRoot 'Deploy-BAndHBRuntime.ps1') -SptRoot $spt -ClientDll $clientDll -ServerDll $serverDll -BackupRoot $BackupRoot

$activeServer = Join-Path $spt 'SPT_Runtime\user\mods\B&A&HB #2 MOD SPT'
Get-ChildItem -LiteralPath $runtime | Copy-Item -Destination $activeServer -Recurse -Force
$sourceCommit = (& git -C $source rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0) { $sourceCommit = 'unavailable' }
$marker = [ordered]@{ mode='private-packnstrap-import'; sourceVersion='2.1.1'; sourceCommit=$sourceCommit; importedAt=(Get-Date).ToString('o') }
$marker | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $activeServer 'packnstrap-local-import.json') -Encoding utf8NoBOM

if ($disableOriginalRuntime) {
    $disabled = Join-Path $BackupRoot ("packnstrap-disabled-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force -Path $disabled | Out-Null
    Move-Item -LiteralPath $serverSource -Destination (Join-Path $disabled 'WTT-PackNStrapServer')
    Move-Item -LiteralPath $clientSource -Destination (Join-Path $disabled 'WTT-PackNStrapClient')
    Write-Output "Pack 'n' Strap runtime disabled and preserved at: $disabled"
} else {
    Write-Output "Pack 'n' Strap runtime was already disabled; import source retained at: $runtimeSourceRoot"
}

Get-FileHash -Algorithm SHA256 -LiteralPath $clientDll,$serverDll | Select-Object Path,Hash
