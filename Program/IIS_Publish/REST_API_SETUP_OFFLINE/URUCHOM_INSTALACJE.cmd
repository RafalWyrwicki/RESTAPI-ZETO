@echo off
setlocal
chcp 65001 >nul
title SIETOM REST API - instalacja

fltmc >nul 2>&1
if errorlevel 1 (
  echo Prosba o uprawnienia administratora...
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-REST_API.ps1"
set EXITCODE=%ERRORLEVEL%
echo.
if not "%EXITCODE%"=="0" echo Instalacja zakonczyla sie bledem %EXITCODE%.
pause
exit /b %EXITCODE%
