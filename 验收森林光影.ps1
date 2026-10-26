param([switch]$SkipBuild,[ValidateSet('Desktop','Mobile')][string]$Quality='Desktop')
$ErrorActionPreference='Stop'
& "$PSScriptRoot/打开美术预览.ps1" -SkipBuild:$SkipBuild -View 50 -FocusCell B3 -DetailScale .10 -Quality $Quality
exit $LASTEXITCODE
