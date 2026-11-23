@echo off
rem Builds Faceit-Simple-cs2 and creates ..\Faceit-Simple-cs2-<ver>.zip. Log: pack.log
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0pack.ps1"
exit /b %errorlevel%
