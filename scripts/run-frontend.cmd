@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-app-service.ps1" -Service Frontend
if errorlevel 1 pause
endlocal
