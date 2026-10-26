param([string]$SourceRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ($SourceRoot -eq $projectRoot) { $SourceRoot = Split-Path $projectRoot -Parent }
$catalogPaths = @('assets/art/origami/catalog.json','assets/art/environment/catalog.json')
$changed = @()
foreach ($catalogRelative in $catalogPaths) {
$catalogPath = Join-Path $projectRoot $catalogRelative
if (-not (Test-Path -LiteralPath $catalogPath)) { continue }
$catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($catalog.sourceIndex) {
    $indexPath = Join-Path $SourceRoot $catalog.sourceIndex
    if (-not (Test-Path -LiteralPath $indexPath)) { $changed += [pscustomobject]@{Asset='environment-index';Source=$catalog.sourceIndex;State='missing'} }
    elseif ((Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $catalog.sourceIndexSha256) {
        $changed += [pscustomobject]@{Asset='environment-index';Source=$catalog.sourceIndex;State='changed-or-new-assets'}
    }
}
foreach ($asset in $catalog.assets) {
    foreach ($component in @($asset.components.PSObject.Properties.Value) + @($asset.dependencies)) {
        if (-not $component) { continue }
        $source = Join-Path $SourceRoot $component.source
        if (-not (Test-Path -LiteralPath $source)) { $changed += [pscustomobject]@{Asset=$asset.id;Source=$component.source;State='missing'}; continue }
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $component.sourceSha256) {
            $changed += [pscustomobject]@{Asset=$asset.id;Source=$component.source;State='changed'}
        }
    }
}
}
$changed | ConvertTo-Json -Depth 4
if ($changed.Count -eq 0) { Write-Output 'No source changes.' }
# Read-only. No automatic commit or upload; each upload requires an explicit user request.
