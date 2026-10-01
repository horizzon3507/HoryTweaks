@echo off
setlocal
if not exist "%~dp0Install-HoryTweaks.ps1" (
    echo Extract the entire HoryTweaks-Installers.zip first. Keep this BAT beside Install-HoryTweaks.ps1.
    pause
    exit /b 1
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-HoryTweaks.ps1" %*
set "result=%errorlevel%"
if "%~1"=="" pause
exit /b %result%
