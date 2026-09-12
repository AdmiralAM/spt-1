$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$lock = Get-Content (Join-Path $root "upstream-lock.json") -Raw | ConvertFrom-Json
$rotation = Get-Content (Join-Path $root "rotation-normal.json") -Raw | ConvertFrom-Json

if ($lock.target.exactValidationBaseline -ne "SPT 4.1.5") {
    throw "Unexpected exact validation baseline"
}

$weights = @(
    $rotation.events.ordinary.weight,
    $rotation.events.ruaf.weight,
    $rotation.events.blackDivision.weight,
    $rotation.events.opposed.weight
)
if (($weights | Measure-Object -Sum).Sum -ne 100) {
    throw "Rotation weights must sum to 100"
}
if (-not $rotation.constraints.supplementOrdinaryPopulation) {
    throw "Special factions must supplement ordinary population"
}
if ($rotation.events.blackDivision.weight -ge $rotation.events.ruaf.weight) {
    throw "Black Division must remain rarer than RUAF"
}
if ($rotation.events.blackDivision.cooldownRaidsAfterAppearance -lt 1) {
    throw "Black Division cooldown must be bounded"
}
if ($lock.packages.Count -ne 8) {
    throw "Expected exact Icebreaker package plus seven declared dependencies"
}

Write-Host "Icebreaker integration foundation PASS: weights=100, dependency contract=$($lock.packages.Count), upstream hashes pending acquisition."
