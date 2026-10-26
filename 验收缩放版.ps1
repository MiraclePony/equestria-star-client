param([switch]$RunChecks,[switch]$SkipBuild)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not $RunChecks) {
    & "$PSScriptRoot/打开美术预览.ps1" -SkipBuild:$SkipBuild -View 50
    exit $LASTEXITCODE
}
$engine=Join-Path $PSScriptRoot '.art-tools/Godot_v4.6.2-stable_mono_win64/Godot_v4.6.2-stable_mono_win64_console.exe'
if (-not $SkipBuild) {
    & $engine --headless --editor --path . --import
    if ($LASTEXITCODE -ne 0) {throw 'Godot导入失败'}
    dotnet build --nologo
    if ($LASTEXITCODE -ne 0) {throw 'C#构建失败'}
}
& $engine --path . --resolution 1760x1000 --script res://tests/RunZoomDetailAcceptance.cs -- --art-clean
if ($LASTEXITCODE -ne 0) {throw '缩放验收失败，查看artifacts/zoom-acceptance.log或终端输出'}
Write-Output '缩放验收通过；截图与zoom-acceptance.json位于artifacts。没有提交或上传。'
