@echo off
title BI Platform - Stop

echo ========================================
echo   BI Platform Stopping...
echo ========================================
echo.

echo [1/3] Stopping backend...
taskkill /f /im "Bi.Api.exe" >nul 2>&1
taskkill /f /fi "WINDOWTITLE eq BI-Backend" >nul 2>&1
for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":5000.*LISTENING"') do (
    echo       Killing PID %%a on port 5000
    taskkill /f /pid %%a >nul 2>&1
)

echo [2/3] Stopping frontend...
taskkill /f /fi "WINDOWTITLE eq BI-Frontend" >nul 2>&1
for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":5180.*LISTENING"') do (
    echo       Killing PID %%a on port 5180
    taskkill /f /pid %%a >nul 2>&1
)

echo [3/3] Stopping Ollama...
taskkill /f /im ollama.exe >nul 2>&1

echo       Waiting for processes to exit...
timeout /t 3 >nul

tasklist /fi "imagename eq Bi.Api.exe" 2>nul | find /i "Bi.Api.exe" >nul 2>&1
if not errorlevel 1 (
    echo.
    echo [WARN] Bi.Api.exe still running, trying PowerShell...
    powershell -NoProfile -Command "Get-Process -Name 'Bi.Api' -ErrorAction SilentlyContinue | Stop-Process -Force"
    timeout /t 2 >nul
    tasklist /fi "imagename eq Bi.Api.exe" 2>nul | find /i "Bi.Api.exe" >nul 2>&1
    if not errorlevel 1 (
        echo [ERROR] Cannot kill Bi.Api.exe. Right-click stop.bat - Run as administrator.
    )
)

echo.
echo ========================================
echo   All services stopped.
echo ========================================
echo.
echo Press any key to close this window...
pause >nul
