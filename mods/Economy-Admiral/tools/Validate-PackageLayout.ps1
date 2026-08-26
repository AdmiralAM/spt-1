param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot
)

$ErrorActionPreference = 'Stop'

$modRoot = Join-Path $PackageRoot 'user/mods/Economy Admiral'
$dll = Join-Path $modRoot 'Economy-Admiral.dll'
$config = Join-Path $modRoot 'config/config.json'
$buildInfo = Join-Path $modRoot 'BUILD_INFO.json'
$runtimeTest = Join-Path $modRoot 'RUNTIME_TEST.md'
$runtimeValidator = Join-Path $modRoot 'Validate-Runtime.ps1'
$parityValidator = Join-Path $modRoot 'Validate-PrimaryParity.ps1'

$required = @($modRoot, $dll, $config, $buildInfo, $runtimeTest, $runtimeValidator, $parityValidator)
$missing = @($required | Where-Object { -not (Test-Path -LiteralPath $_) })
if ($missing.Count -gt 0) {
    throw "Economy Admiral package layout invalid. Missing: $($missing -join '; ')"
}

$rootEntries = @(Get-ChildItem -LiteralPath $PackageRoot -Force)
if ($rootEntries.Count -ne 1 -or $rootEntries[0].Name -ne 'user' -or -not $rootEntries[0].PSIsContainer) {
    throw "Economy Admiral package root must contain exactly one top-level directory: user"
}

$modDlls = @(Get-ChildItem -LiteralPath $modRoot -Filter '*.dll' -File)
if ($modDlls.Count -lt 1) {
    throw "Economy Admiral mod root contains no assembly; SPT ModLoader would fail with 'No Assemblies found in path'."
}

$info = Get-Content -LiteralPath $buildInfo -Raw | ConvertFrom-Json
if ($info.Product -ne 'Economy Admiral') {
    throw "BUILD_INFO Product mismatch: $($info.Product)"
}
if ($info.InstallRelativePath -ne 'user/mods/Economy Admiral') {
    throw "BUILD_INFO InstallRelativePath mismatch: $($info.InstallRelativePath)"
}

Write-Host "Economy Admiral drop-in package layout PASS: user/mods/Economy Admiral" -ForegroundColor Green
