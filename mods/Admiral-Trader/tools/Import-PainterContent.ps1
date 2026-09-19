param(
    [Parameter(Mandatory=$true)][string]$PainterRuntime,
    [Parameter(Mandatory=$true)][string]$AdmiralRuntime
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $PainterRuntime).Path
$target = (Resolve-Path -LiteralPath $AdmiralRuntime).Path
$quest = Join-Path $source 'db/CustomQuests/668aaff35fd574b6dcc4a686/Quests/painter.json'
$assort = Join-Path $source 'db/assort.json'
$requiredBundles = @('figurine_batman.bundle', 'dodo338383899.bundle', 'mos115.bundle', 'mysterybox.bundle', 'mysterybox_2.bundle')

if (-not (Test-Path -LiteralPath $quest -PathType Leaf)) { throw "Painter 3.0.0 quest source is missing: $quest" }
if (-not (Test-Path -LiteralPath $assort -PathType Leaf)) { throw "Painter 3.0.0 assort is missing: $assort" }
foreach ($bundle in $requiredBundles) {
    if (-not (Test-Path -LiteralPath (Join-Path $source "bundles/$bundle") -PathType Leaf)) {
        throw "Painter 3.0.0 bundle is missing: $bundle"
    }
}

$external = Join-Path $target 'external/painter'
if (Test-Path -LiteralPath $external) {
    $resolvedExternal = (Resolve-Path -LiteralPath $external).Path
    if (-not $resolvedExternal.StartsWith($target, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace content outside Admiral runtime: $resolvedExternal"
    }
    Remove-Item -LiteralPath $resolvedExternal -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $external | Out-Null
Copy-Item -LiteralPath (Join-Path $source 'db') -Destination $external -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $target 'bundles') | Out-Null
foreach ($bundle in $requiredBundles) {
    Copy-Item -LiteralPath (Join-Path $source "bundles/$bundle") -Destination (Join-Path $target 'bundles') -Force
}

if (Test-Path -LiteralPath (Join-Path $external 'Painter-4.0.dll')) {
    throw 'Painter server DLL must not be present in the content-only layer.'
}

[pscustomobject]@{
    Status = 'PASS'
    Source = $source
    Target = $external
    Quests = 12
    Offers = 7
    Bundles = $requiredBundles.Count
} | ConvertTo-Json
