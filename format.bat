@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0format.ps1" -Root "%~dp0"
pause
