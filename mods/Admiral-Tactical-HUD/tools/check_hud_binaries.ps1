param(
    [Parameter(Mandatory = $true)][string]$Compact,
    [Parameter(Mandatory = $true)][string]$FullCensus
)

$ErrorActionPreference = 'Stop'
$expectedResources = @(
    'blackdivision.png', 'boss.png', 'btr.png', 'cultist.png', 'faction.png',
    'goons.png', 'guard.png', 'infected.png', 'other.png', 'pmc.png',
    'raider.png', 'rogue.png', 'scav.png', 'total.png', 'wedge.png'
) | ForEach-Object { "AdmiralTacticalHUD.BotCensus.$_" }
$reserve = 'AdmiralTacticalHUD.Reserve.hud-sprites.png'

function Test-HudAssembly([string]$Path, [string]$ExpectedName, [string]$ExpectedGuid) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "HUD assembly missing: $Path" }
    $assembly = [System.Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $Path).Path)
    if ($assembly.GetName().Name -ne $ExpectedName) {
        throw "Assembly identity mismatch for $Path: $($assembly.GetName().Name)"
    }
    $resources = @($assembly.GetManifestResourceNames())
    foreach ($name in @($expectedResources) + $reserve) {
        if ($resources -notcontains $name) { throw "$ExpectedName missing embedded resource: $name" }
    }
    $binaryText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path)))
    if (-not $binaryText.Contains($ExpectedGuid)) { throw "$ExpectedName missing plugin GUID: $ExpectedGuid" }
    [pscustomobject]@{ Name = $assembly.GetName().Name; Guid = $ExpectedGuid; Resources = $resources.Count }
}

$compactResult = Test-HudAssembly $Compact 'Admiral Tactical HUD' 'com.admiralam.tacticalhud'
$fullResult = Test-HudAssembly $FullCensus 'Admiral Tactical HUD Full Census' 'com.admiralam.tacticalhud.fullcensus'
if ($compactResult.Name -eq $fullResult.Name -or $compactResult.Guid -eq $fullResult.Guid) {
    throw 'Compact and Full Census identities must be unique'
}
$compactResult
$fullResult
Write-Host '[OK] Compact and Full Census binary identities and embedded resources validated'
