@echo off
rem Claude sunlit papercraft lookdev: import, build, render fixed-camera comparisons.
rem Output: artifacts\claude-sunlit\*.png, report.json, run.log
chcp 65001 >nul
cd /d "%~dp0"
set GODOT=.art-tools\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64_console.exe
set OUT=artifacts\claude-sunlit
if not exist "%OUT%" mkdir "%OUT%"
echo [%date% %time%] start > "%OUT%\run.log"
echo [import] >> "%OUT%\run.log"
"%GODOT%" --headless --editor --path . --import >> "%OUT%\run.log" 2>&1
echo [build] >> "%OUT%\run.log"
call dotnet build --nologo >> "%OUT%\run.log" 2>&1
if errorlevel 1 (
  echo BUILD_FAILED >> "%OUT%\run.log"
  echo BUILD_FAILED > "%OUT%\status.txt"
  exit /b 1
)
echo [render] >> "%OUT%\run.log"
"%GODOT%" --path . --rendering-method forward_plus --resolution 1760x1000 --script res://tests/RunClaudeSunlitLookDev.cs >> "%OUT%\run.log" 2>&1
echo [%date% %time%] exit %errorlevel% >> "%OUT%\run.log"
echo DONE > "%OUT%\status.txt"
