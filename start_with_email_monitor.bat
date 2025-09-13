@echo off
echo Starting EQ Log Automator with Email Monitoring...
echo.

REM Start EmailMonitor in background
echo Starting EmailMonitor...
start /B EmailMonitor.exe config.yaml

REM Wait a moment for EmailMonitor to start
timeout /t 2 /nobreak >nul

REM Start LogEventProcessor
echo Starting LogEventProcessor...
LogEventProcessor.exe

REM Clean up - stop EmailMonitor when LogEventProcessor exits
echo Stopping EmailMonitor...
taskkill /f /im EmailMonitor.exe >nul 2>&1

echo.
echo EQ Log Automator stopped.
pause
