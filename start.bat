@echo off
title BI Platform - Start
set "BASEDIR=%~dp0"

echo ========================================
echo   BI Platform Starting...
echo ========================================
echo.

echo [1/5] Killing old processes...
taskkill /f /im "Bi.Api.exe" >nul 2>&1
taskkill /f /fi "WINDOWTITLE eq BI-Backend" >nul 2>&1
taskkill /f /fi "WINDOWTITLE eq BI-Frontend" >nul 2>&1
for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":5000.*LISTENING"') do taskkill /f /pid %%a >nul 2>&1
for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":5180.*LISTENING"') do taskkill /f /pid %%a >nul 2>&1
tasklist /fi "imagename eq Bi.Api.exe" 2>nul | find /i "Bi.Api.exe" >nul 2>&1
if not errorlevel 1 (
    echo       [WARN] taskkill failed, trying PowerShell...
    powershell -NoProfile -Command "Get-Process -Name 'Bi.Api' -ErrorAction SilentlyContinue | Stop-Process -Force"
)
echo       Waiting for processes to exit...
timeout /t 5 >nul
tasklist /fi "imagename eq Bi.Api.exe" 2>nul | find /i "Bi.Api.exe" >nul 2>&1
if not errorlevel 1 (
    echo       [ERROR] Bi.Api.exe still running. Right-click start.bat - Run as administrator.
    pause
    exit /b 1
)

echo [2/5] Starting Ollama BGE-M3...
tasklist /fi "imagename eq ollama.exe" 2>nul | find /i "ollama.exe" >nul 2>&1
if errorlevel 1 (
    echo       Starting Ollama service...
    start "Ollama" /min cmd /c "ollama serve"
    timeout /t 3 >nul
)
ollama run bge-m3 "test" >nul 2>&1

echo [3/5] Building backend...
pushd "%BASEDIR%backend"
dotnet build Bi.Api -c Debug >nul 2>&1
if errorlevel 1 (
    echo       [ERROR] Build failed:
    dotnet build Bi.Api -c Debug
    popd
    pause
    exit /b 1
)
echo       Build OK. Starting backend (port 5000)...
start "BI-Backend" cmd /k "dotnet run --project Bi.Api --no-build --urls http://localhost:5000"
popd

echo [4/5] Waiting for backend...
timeout /t 8 >nul

echo [5/5] Starting frontend (port 5180)...
pushd "%BASEDIR%frontend"
start "BI-Frontend" cmd /k "npm run dev"
popd

timeout /t 5 >nul

echo.
echo ========================================
echo   Started!
echo   Backend:  http://localhost:5000
echo   Frontend: http://localhost:5180
echo   Swagger:  http://localhost:5000/swagger
echo   Ollama:   http://localhost:11434
echo ========================================
echo.
echo Press any key to close this window...
pause >nul
