param(
    [ValidateSet('Desktop','Mobile','Compatibility')][string]$Quality='Desktop',
    [ValidateRange(0,8)][int]$PaperPreset=5,
    [switch]$Reduced,
    [switch]$ShowPanel,
    [switch]$SkipBuild,
    [switch]$AuditMotion,
    [ValidateSet('preview','acceptance','film','benchmark')][string]$Mode='preview',
    [string]$GodotPath=''
)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
if(-not $GodotPath){$GodotPath=Join-Path $PSScriptRoot '.art-tools/Godot_v4.6.2-stable_mono_win64/Godot_v4.6.2-stable_mono_win64_console.exe'}
if(-not (Test-Path -LiteralPath $GodotPath -PathType Leaf)){throw '请指定 Godot 4.6.2 .NET 可执行文件：-GodotPath'}
if(-not $SkipBuild){
    & $GodotPath --headless --editor --path . --import
    if($LASTEXITCODE -ne 0){throw '资源导入失败'}
    dotnet build --nologo
    if($LASTEXITCODE -ne 0){throw 'C#构建失败'}
}
$method=switch($Quality){'Desktop'{'forward_plus'} 'Mobile'{'mobile'} default{'gl_compatibility'}}
$engineArgs=@('--path','.','--rendering-method',$method,'--resolution','1760x1000')
$artArgs=@('--art-view','50','--paper-preset',"$PaperPreset")
if($Reduced){$artArgs+='--paper-reduced'}
if($Mode -eq 'preview'){
    $engineArgs+='res://scenes/art/OrigamiGallery.tscn'
    $artArgs+=@('--art-focus','B3','--art-scale','0.10')
    if($ShowPanel){$artArgs+='--art-show-panel'}
    if($AuditMotion){$artArgs+=@('--art-motion-audit','res://artifacts/paper-v5-live-motion.json')}
}else{
    $engineArgs+=@('--script','res://tests/RunPaperV5Acceptance.cs')
    $artArgs+=@('--v5-mode',$Mode)
    if($Mode -eq 'acceptance'){$engineArgs+=@('--fixed-fps','30')}
    if($Mode -eq 'film'){$engineArgs+=@('--fixed-fps','30','--write-movie','artifacts/paper-v5-actual-runtime.avi')}
}
# Default is the normal interactive Gallery, with automatic animation and collapsed
# settings. Audit samples this same window for 8.4 s without driving its clock.
& $GodotPath @engineArgs -- @artArgs
if($LASTEXITCODE -ne 0){throw "Godot退出异常：$LASTEXITCODE"}
