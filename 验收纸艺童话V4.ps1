param([switch]$RunChecks,[switch]$Benchmark,[switch]$Record,[switch]$SkipBuild,[ValidateSet('Desktop','Mobile','Compatibility')][string]$Quality='Desktop',[ValidateRange(0,4)][int]$PaperPreset=3,[switch]$Reduced)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
$engine=Join-Path $PSScriptRoot '.art-tools/Godot_v4.6.2-stable_mono_win64/Godot_v4.6.2-stable_mono_win64_console.exe'
$method=switch($Quality){'Mobile'{'mobile'}'Compatibility'{'gl_compatibility'}default{'forward_plus'}}
if(-not ($RunChecks -or $Benchmark -or $Record)){
    & "$PSScriptRoot/打开美术预览.ps1" -SkipBuild:$SkipBuild -Quality $Quality -PaperPreset $PaperPreset -View 50 -Reduced:$Reduced
    exit $LASTEXITCODE
}
if(-not $SkipBuild){
    & $engine --headless --editor --path . --import
    if($LASTEXITCODE -ne 0){throw 'Godot导入失败'}
    dotnet build --nologo
    if($LASTEXITCODE -ne 0){throw 'C#构建失败'}
}
$engineArgs=@('--path','.','--rendering-method',$method,'--resolution','1760x1000','--script','res://tests/RunPaperV4Acceptance.cs')
$mode=if($Record){'film'}elseif($Benchmark){'benchmark'}else{'acceptance'}
if($Record){$engineArgs+=@('--write-movie','artifacts/paper-v4-actual-runtime.avi','--fixed-fps','30')}
$engineArgs+=@('--','--art-view','50','--paper-preset',"$PaperPreset",'--v4-mode',$mode)
if($Benchmark){$engineArgs+=@('--v4-look',"$PaperPreset",'--v4-seconds','30')}
if($Reduced){$engineArgs+='--paper-reduced'}
& $engine @engineArgs *> "artifacts/paper-v4-$method-$mode-launcher.log"
if($LASTEXITCODE -ne 0){throw 'V4检查失败，见artifacts日志'}
Write-Output 'V4本地执行完成；没有提交、上传或部署。录像是实际工程固定步长录制，不代表实测FPS。'
