param([switch]$RunChecks,[switch]$SkipBuild,[ValidateSet('Desktop','Mobile','Compatibility')][string]$Quality='Desktop',[ValidateRange(0,4)][int]$PaperPreset=3)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
if(-not $RunChecks) {
    & "$PSScriptRoot/打开美术预览.ps1" -SkipBuild:$SkipBuild -Quality $Quality -PaperPreset $PaperPreset -View 50
    exit $LASTEXITCODE
}
$engine=Join-Path $PSScriptRoot '.art-tools/Godot_v4.6.2-stable_mono_win64/Godot_v4.6.2-stable_mono_win64_console.exe'
if(-not $SkipBuild) {
    & $engine --headless --editor --path . --import
    if($LASTEXITCODE -ne 0) {throw 'Godot导入失败'}
    dotnet build --nologo
    if($LASTEXITCODE -ne 0) {throw '构建失败'}
}
foreach($method in @('forward_plus','mobile','gl_compatibility')) {
    $engineArgs=@('--path','.','--rendering-method',$method,'--resolution','1760x1000','--script','res://tests/RunPaperLookAcceptance.cs','--','--art-view','50','--paper-preset','0')
    & $engine @engineArgs *> "artifacts/paper-v3-$method.log"
    if($LASTEXITCODE -ne 0) {throw "$method审核验收失败，见artifacts对应日志"}
}
& $engine --path . --script res://tests/RunZoomDetailAcceptance.cs -- --art-clean *> artifacts/paper-v3-zoom.log
if($LASTEXITCODE -ne 0) {throw '缩放验收失败'}
Write-Output '审核版验收完成；结果在artifacts/paper-v3-*。没有提交或上传。'
