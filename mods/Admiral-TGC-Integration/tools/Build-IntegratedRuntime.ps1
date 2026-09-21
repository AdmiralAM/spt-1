param(
    [Parameter(Mandatory=$true)][string]$UpstreamRuntime,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [string]$Configuration = 'Release',
    [string]$PythonPath
)

$ErrorActionPreference = 'Stop'
$module = Split-Path $PSScriptRoot -Parent
$project = Join-Path $module 'server/Admiral-TGC-Integration.csproj'
if (-not $PythonPath) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if (-not $pythonCommand) { throw 'Python was not found. Pass -PythonPath explicitly.' }
    $PythonPath = $pythonCommand.Source
}
if (-not (Test-Path $PythonPath -PathType Leaf)) { throw "Python is missing: $PythonPath" }
& $PythonPath (Join-Path $PSScriptRoot 'audit_upstream.py') $UpstreamRuntime | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Upstream TGC audit failed.' }

dotnet build $project -c $Configuration --nologo --configfile (Join-Path $module 'NuGet.Config')
if ($LASTEXITCODE -ne 0) { throw 'Integrated TGC server build failed.' }

$destination = Join-Path $OutputRoot 'SPT_Runtime/user/mods/TGC-NG'
$resolvedOutput = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$resolvedDestination = [IO.Path]::GetFullPath($destination)
if (-not $resolvedDestination.StartsWith("$resolvedOutput\", [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe output destination: $resolvedDestination"
}
if (Test-Path $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -Path (Join-Path $UpstreamRuntime '*') -Destination $destination -Recurse -Force

$stockDll = Join-Path $destination 'TGC-NG.dll'
if (Test-Path $stockDll) { Remove-Item -LiteralPath $stockDll -Force }
$builtDll = Join-Path $module "server/bin/$Configuration/net10.0/TGC-NG.dll"
Copy-Item -LiteralPath $builtDll -Destination $stockDll -Force

$forbiddenPainter = Get-ChildItem -Path $OutputRoot -Recurse -File -Filter 'Painter-4.0.dll' -ErrorAction SilentlyContinue
if ($forbiddenPainter) { throw 'Painter DLL is present in the assembled runtime.' }

$hash = (Get-FileHash -Algorithm SHA256 $stockDll).Hash
[pscustomobject]@{
    Status = 'PASS'
    Runtime = $destination
    IntegratedDllSha256 = $hash
} | ConvertTo-Json
