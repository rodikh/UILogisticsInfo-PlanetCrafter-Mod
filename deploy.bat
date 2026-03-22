@echo off
REM LogisticsInfo Deploy - double-click to build and deploy
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy.ps1"
if %errorlevel% neq 0 (
    pause
)
