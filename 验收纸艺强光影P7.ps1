param(
    [switch]$SkipBuild,
    [ValidateSet('Desktop','Mobile')][string]$Quality='Desktop'
)
# Papercraft NPR preset 7 preview entry.
# Same scene / assets / camera as the V5 entry; only the default art preset differs (7).
# Run 验收纸艺世界V5.ps1 instead to fall back to the untouched V5 look.
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
$godot=Join-Path $PSScriptRoot '.art-tools/Godot_v4.6.2-stable_mono_win64/Godot_v4.6.2-stable_mono_win64_console.exe'
if(-not (Test-Path -LiteralPath $godot -PathType Leaf)){throw 'Missing Godot 4.6.2 .NET executable'}
if(-not $SkipBuild){
    & $godot --headless --editor --path . --import
    if($LASTEXITCODE -ne 0){throw 'Asset import failed'}
    dotnet build --nologo
    if($LASTEXITCODE -ne 0){throw 'C# build failed'}
}
$method=switch($Quality){'Desktop'{'forward_plus'} default{'mobile'}}
$engine=@('--path','.','--rendering-method',$method,'--resolution','1760x1000')
$engine+='res://scenes/art/OrigamiGallery.tscn'
$art=@('--art-view','50','--paper-preset','7','--art-focus','B3','--art-scale','0.10')
& $godot @engine -- @art
if($LASTEXITCODE -ne 0){throw "Godot exited with $LASTEXITCODE"}
