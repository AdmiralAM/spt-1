param(
    [Parameter(Mandatory = $true)]
    [string]$ProfilePath,

    [string]$OutputPath,

    [string]$ManifestPath = (Join-Path $PSScriptRoot 'persistent-identities.json')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ProfilePath -PathType Leaf)) {
    throw "Profile not found: $ProfilePath"
}
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Persistent identity manifest not found: $ManifestPath"
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$templateIds = @{}
foreach ($id in $manifest.templateIds) { $templateIds[[string]$id] = $true }

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $directory = Split-Path -Parent $ProfilePath
    $name = [IO.Path]::GetFileNameWithoutExtension($ProfilePath)
    $extension = [IO.Path]::GetExtension($ProfilePath)
    $OutputPath = Join-Path $directory ($name + '.bahb-clean' + $extension)
}

$profile = Get-Content -LiteralPath $ProfilePath -Raw | ConvertFrom-Json
$allObjects = New-Object 'System.Collections.Generic.List[object]'

function Visit-Node([object]$Node) {
    if ($null -eq $Node) { return }
    if ($Node -is [string] -or $Node -is [ValueType]) { return }

    if ($Node -is [System.Collections.IEnumerable] -and -not ($Node -is [pscustomobject])) {
        foreach ($child in $Node) { Visit-Node $child }
        return
    }

    if ($Node -is [pscustomobject]) {
        $script:allObjects.Add($Node)
        foreach ($property in $Node.PSObject.Properties) { Visit-Node $property.Value }
    }
}

Visit-Node $profile

# Recovery data is untrusted. Healthy SPT profiles use globally unique item instance
# IDs, but a malformed/mixed-mod profile can violate that invariant. Cascade authority
# is granted only to IDs that occur exactly once anywhere in the serialized profile.
$idCounts = @{}
foreach ($obj in $allObjects) {
    $idProperty = $obj.PSObject.Properties['_id']
    if ($null -eq $idProperty) { continue }
    $id = [string]$idProperty.Value
    if ([string]::IsNullOrWhiteSpace($id)) { continue }
    if ($idCounts.ContainsKey($id)) { $idCounts[$id] = [int]$idCounts[$id] + 1 }
    else { $idCounts[$id] = 1 }
}

function Test-UniqueInstanceId([string]$Id) {
    return (-not [string]::IsNullOrWhiteSpace($Id)) -and $idCounts.ContainsKey($Id) -and ([int]$idCounts[$Id] -eq 1)
}

$removedIds = @{}
$directOwned = 0
foreach ($obj in $allObjects) {
    $tplProperty = $obj.PSObject.Properties['_tpl']
    if ($null -eq $tplProperty) { continue }
    $tpl = [string]$tplProperty.Value
    if (-not $templateIds.ContainsKey($tpl)) { continue }

    $directOwned++
    $idProperty = $obj.PSObject.Properties['_id']
    $id = if ($null -ne $idProperty) { [string]$idProperty.Value } else { $null }
    if (Test-UniqueInstanceId $id) {
        $removedIds[$id] = $true
    }
}

# Expand the removal set through inventory parent edges and common serialized item
# references. The referenced owner ID must already be cardinality-proven unique. A
# removed descendant with an ambiguous own ID is removed because its unique parent
# proves ownership, but that ambiguous ID is not promoted into further cascade authority.
do {
    $changed = $false
    foreach ($obj in $allObjects) {
        $idProperty = $obj.PSObject.Properties['_id']
        $id = if ($null -ne $idProperty) { [string]$idProperty.Value } else { $null }
        if ((Test-UniqueInstanceId $id) -and $removedIds.ContainsKey($id)) { continue }

        $parentProperty = $obj.PSObject.Properties['parentId']
        $itemProperty = $obj.PSObject.Properties['itemId']
        $parentId = if ($null -ne $parentProperty) { [string]$parentProperty.Value } else { $null }
        $itemId = if ($null -ne $itemProperty) { [string]$itemProperty.Value } else { $null }

        if (($parentId -and $removedIds.ContainsKey($parentId)) -or ($itemId -and $removedIds.ContainsKey($itemId))) {
            if ((Test-UniqueInstanceId $id) -and -not $removedIds.ContainsKey($id)) {
                $removedIds[$id] = $true
                $changed = $true
            }
        }
    }
} while ($changed)

$locations = New-Object 'System.Collections.Generic.List[string]'
$removedObjects = 0

function Should-RemoveObject([object]$Object) {
    if (-not ($Object -is [pscustomobject])) { return $false }

    $tplProperty = $Object.PSObject.Properties['_tpl']
    if ($null -ne $tplProperty -and $templateIds.ContainsKey([string]$tplProperty.Value)) { return $true }

    $idProperty = $Object.PSObject.Properties['_id']
    if ($null -ne $idProperty -and $removedIds.ContainsKey([string]$idProperty.Value)) { return $true }

    $parentProperty = $Object.PSObject.Properties['parentId']
    if ($null -ne $parentProperty -and $removedIds.ContainsKey([string]$parentProperty.Value)) { return $true }

    $itemProperty = $Object.PSObject.Properties['itemId']
    if ($null -ne $itemProperty -and $removedIds.ContainsKey([string]$itemProperty.Value)) { return $true }

    return $false
}

function Rewrite-Node([object]$Node, [string]$Path) {
    if ($null -eq $Node) { return }
    if ($Node -is [string] -or $Node -is [ValueType]) { return }

    if ($Node -is [pscustomobject]) {
        foreach ($property in @($Node.PSObject.Properties)) {
            $value = $property.Value
            if ($value -is [System.Array]) {
                $kept = New-Object 'System.Collections.Generic.List[object]'
                for ($i = 0; $i -lt $value.Count; $i++) {
                    $child = $value[$i]
                    $childPath = "$Path.$($property.Name)[$i]"
                    if (Should-RemoveObject $child) {
                        $script:removedObjects++
                        $script:locations.Add($childPath)
                        continue
                    }
                    Rewrite-Node $child $childPath
                    $kept.Add($child)
                }
                $property.Value = $kept.ToArray()
            }
            else {
                Rewrite-Node $value "$Path.$($property.Name)"
            }
        }
    }
}

Rewrite-Node $profile '$'

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$profile | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $OutputPath -Encoding UTF8

Write-Host "[B&A&HB #2 MOD SPT] profile recovery complete"
Write-Host "  direct mod items detected: $directOwned"
Write-Host "  serialized objects removed: $removedObjects"
Write-Host "  cleaned profile: $OutputPath"
if ($locations.Count -gt 0) {
    Write-Host '  removed locations:'
    foreach ($location in $locations) { Write-Host "    $location" }
}