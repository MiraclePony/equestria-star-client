param([string]$GodotPath = '', [switch]$SkipBuild, [int]$View = 50, [ValidateSet('Mobile','Desktop','Compatibility')][string]$Quality = 'Desktop', [switch]$Photo, [string]$FocusCell = '', [float]$DetailScale = 0,[ValidateRange(0,9)][int]$PaperPreset=5,[switch]$Reduced,[switch]$Pawns)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not $GodotPath) {
    $localEngine=Join-Path $PSScriptRoot '.art-tools/Godot_v4.6.2-stable_mono_win64/Godot_v4.6.2-stable_mono_win64.exe'
    if (Test-Path -LiteralPath $localEngine) { $GodotPath=$localEngine }
    else { throw '请通过 -GodotPath 指定 Godot 4.6.2 .NET 可执行文件。' }
}
if (-not $SkipBuild) {
    & $GodotPath --headless --editor --path . --import
    if ($LASTEXITCODE -ne 0) {throw '资源导入失败'}
    dotnet build --nologo
    if ($LASTEXITCODE -ne 0) {throw 'C#构建失败'}
}
$renderMethod=switch($Quality) {'Desktop' {'forward_plus'} 'Compatibility' {'gl_compatibility'} default {'mobile'}}
$artArgs=@('--art-view',"$View",'--paper-preset',"$PaperPreset")
if($Reduced){$artArgs+='--paper-reduced'}
if($Pawns){$artArgs+='--art-pawns'}
if ($Quality -eq 'Desktop') { $artArgs+='--art-quality' }
if ($Photo) { $artArgs+='--art-diorama' }
if ($FocusCell) { $artArgs+=@('--art-focus',$FocusCell) }
if ($DetailScale -gt 0) { $artArgs+=@('--art-scale',$DetailScale.ToString([Globalization.CultureInfo]::InvariantCulture)) }
& $GodotPath --path . --rendering-method $renderMethod res://scenes/art/OrigamiGallery.tscn -- @artArgs
