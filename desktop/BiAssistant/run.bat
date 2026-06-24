@echo off
title BI 智能助手
cd /d "%~dp0"

echo ========================================
echo   BI 智能助手 - 桌面悬浮球
echo ========================================
echo.

REM 检查是否已编译
if not exist "bin\Debug\net9.0-windows\BiAssistant.exe" (
    echo [INFO] 首次运行，正在编译...
    dotnet build
    if errorlevel 1 (
        echo [ERROR] 编译失败！
        pause
        exit /b 1
    )
)

echo [INFO] 正在启动 BI 智能助手...
echo [INFO] 悬浮球将出现在屏幕右下角
echo [INFO] 点击悬浮球打开智能分析窗口
echo [INFO] 右键系统托盘图标可退出程序
echo.

start "" "bin\Debug\net9.0-windows\BiAssistant.exe"

echo [OK] 已启动！可以关闭此窗口。
timeout /t 3 >nul

