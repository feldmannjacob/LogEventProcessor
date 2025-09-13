@echo off
echo Starting Email Monitoring System...
echo.

REM Start EmailMonitor in background
start "EmailMonitor" /min EmailMonitor.exe

REM Wait a moment for EmailMonitor to start
timeout /t 2 /nobreak >nul

REM Start LogEventProcessor in foreground
echo Starting LogEventProcessor...
LogEventProcessor.exe

REM Cleanup - close EmailMonitor when LogEventProcessor exits
taskkill /f /im EmailMonitor.exe >nul 2>&1
echo.
echo Email monitoring system stopped.
pause
