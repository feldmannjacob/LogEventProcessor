@echo off
echo Starting Log Event Processor...
echo.
cd /d "%~dp0"
..\x64\Debug\LogEventProcessor.exe
pause
