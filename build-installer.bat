@echo off
rem Double-click to build BaixakiSetup.exe (needs internet the first time).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1"
echo.
pause
