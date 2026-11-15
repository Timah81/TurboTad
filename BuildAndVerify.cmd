@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0BuildAndVerify.ps1"
if errorlevel 1 (echo Build/test FAILED.) else (echo Build/test completed.)
pause
